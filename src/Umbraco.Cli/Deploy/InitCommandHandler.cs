using System.Text;
using System.Text.Json;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Deploy.ProjectDetection;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Json;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Deploy;

/// <summary>
/// <c>umbraco deploy init</c>: ensures an ApiSecret is configured and Environments are added for Umbraco Deploy.
/// </summary>
internal sealed class InitCommandHandler
{
    private readonly IEnvironment _environment;
    private readonly DeployPackageDetector _detector;
    private readonly IEnumerable<IDeployConfigurationWriter> _writers;
    private readonly IPrompter _prompter;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public InitCommandHandler(
        IEnvironment environment,
        DeployPackageDetector detector,
        IEnumerable<IDeployConfigurationWriter> writers,
        IPrompter prompter,
        IOutput output,
        IReporter reporter)
    {
        _environment = environment;
        _detector = detector;
        _writers = writers;
        _prompter = prompter;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(InitCommandSettings settings, CancellationToken cancellationToken)
    {
        bool interactive = settings.NonInteractive is false && _environment.IsInputRedirected is false && _environment.IsErrorRedirected is false;

        string projectPath = ProjectLocator.Locate(settings.Project, _environment.CurrentDirectory);
        DetectedPackage detected = await _detector.DetectAsync(projectPath, interactive, cancellationToken);
        _reporter.Info($"Detected {detected.PackageId} {detected.Version} in {Path.GetFileName(projectPath)}.");

        List<EnvironmentSpec>? environments = ParseEnvironments(settings.Environments);
        Guid? projectId = ValidatePackageOptions(settings, detected.Package);

        IDeployConfigurationWriter writer = _writers.Single(candidate => candidate.Package == detected.Package);
        string filePath = Path.Combine(Path.GetDirectoryName(projectPath)!, writer.FileName);
        JsonDocumentEditor document = Load(filePath);

        string? currentEnvironment = await ResolveCurrentEnvironmentAsync(settings, detected.Package, environments, document, interactive, cancellationToken);

        byte[] original = document.ToBytes();
        InitOutcome outcome = writer.Apply(document, new InitRequest(environments, currentEnvironment, projectId, settings.RotateSecret));
        bool changed = File.Exists(filePath) is false || original.AsSpan().SequenceEqual(document.ToBytes()) is false;
        if (changed is false)
        {
            // Setting the same values again is not a change worth reporting
            outcome.Changes.Clear();
        }
        else if (settings.DryRun is false)
        {
            AtomicFile.WriteAllBytes(filePath, document.ToBytes());
        }

        foreach (string warning in outcome.Warnings)
        {
            _reporter.Warning(warning);
        }

        var result = new InitResult
        {
            Package = detected.PackageId,
            PackageVersion = detected.Version,
            File = filePath,
            DryRun = settings.DryRun,
            Environments = outcome.Environments,
            Changes = outcome.Changes,
            ApiSecretGenerated = outcome.GeneratedApiSecret is not null,
            ApiSecret = settings.DryRun ? null : outcome.GeneratedApiSecret,
        };

        _output.Write(result, CliJsonContext.Default.InitResult, _ => FormatText(result, outcome));
        return ExitCodes.Success;
    }

    private static List<EnvironmentSpec>? ParseEnvironments(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        List<EnvironmentSpec> environments = values.Select(EnvironmentSpec.Parse).ToList();
        string? duplicate = environments
            .GroupBy(environment => environment.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        return duplicate is null ? environments : throw new UsageException($"The environment name '{duplicate}' is used more than once.");
    }

    private static JsonDocumentEditor Load(string filePath)
    {
        if (File.Exists(filePath) is false)
        {
            return JsonDocumentEditor.Parse("{}");
        }

        try
        {
            return JsonDocumentEditor.Parse(File.ReadAllBytes(filePath));
        }
        catch (JsonException exception)
        {
            throw new CliException($"'{filePath}' is not a valid JSON object: {exception.Message}", ExitCodes.Error, innerException: exception);
        }
    }

    private static string FormatText(InitResult result, InitOutcome outcome)
    {
        var text = new StringBuilder();
        text.AppendLine(result.DryRun ? $"Dry run: {result.File} was not changed." : outcome.Changes.Count > 0 ? $"Updated {result.File}." : $"{result.File} is already up to date.");

        foreach (string change in outcome.Changes)
        {
            text.AppendLine($"  {change}");
        }

        foreach (string note in outcome.Notes)
        {
            text.AppendLine($"  {note}");
        }

        foreach (WorkspaceEntry environment in result.Environments)
        {
            text.AppendLine($"  Environment {environment.Name} ({environment.Type}): {environment.Url}");
        }

        if (outcome.GeneratedApiSecret is not null)
        {
            text.AppendLine();
            text.AppendLine("New ApiSecret (every environment must be configured with this same value):");
            text.Append(result.DryRun ? ApiSecret.Masked : outcome.GeneratedApiSecret);
        }

        return text.ToString().TrimEnd();
    }

    private Guid? ValidatePackageOptions(InitCommandSettings settings, DeployPackage package)
    {
        if (package == DeployPackage.OnPremises)
        {
            if (settings.ProjectId is not null)
            {
                throw new UsageException("--project-id only applies to Umbraco Deploy Cloud.");
            }

            return null;
        }

        if (settings.CurrentEnvironment is not null)
        {
            throw new UsageException("--current-environment only applies to Umbraco Deploy On-Premises; Umbraco Cloud determines the current environment itself.");
        }

        string? projectId = settings.ProjectId ?? _environment.GetVariable(Cloud.CloudCredentialsResolver.ProjectIdVariable);
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        return Guid.TryParse(projectId, out Guid parsed) ? parsed : throw new UsageException($"The project ID '{projectId}' is not a GUID.");
    }

    private async Task<string?> ResolveCurrentEnvironmentAsync(
        InitCommandSettings settings,
        DeployPackage package,
        List<EnvironmentSpec>? environments,
        JsonDocumentEditor document,
        bool interactive,
        CancellationToken cancellationToken)
    {
        if (package != DeployPackage.OnPremises)
        {
            return null;
        }

        IReadOnlyList<string> names = environments?.Select(environment => environment.Name).ToList()
            ?? WorkspaceList.Read(document.GetNode("Umbraco", "Deploy", "Project", "Workspaces")).Select(entry => entry.Name).ToList();

        if (settings.CurrentEnvironment is not null)
        {
            return names.FirstOrDefault(name => string.Equals(name, settings.CurrentEnvironment, StringComparison.OrdinalIgnoreCase))
                ?? throw new UsageException(
                    $"The current environment '{settings.CurrentEnvironment}' is not one of the environments ({string.Join(", ", names)}).",
                    "Use the name of one of the --environment values.");
        }

        if (environments is null)
        {
            return null;
        }

        if (interactive)
        {
            return await _prompter.SelectAsync("Which environment is this site?", names, cancellationToken);
        }

        throw new UsageException("--current-environment is required when --environment is given.", $"Add --current-environment with one of: {string.Join(", ", names)}.");
    }
}
