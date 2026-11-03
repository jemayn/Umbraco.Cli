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
    public const string BaselineSecretVariable = "UMBRACO_DEPLOY_BASELINE_SECRET";

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
        BaselineOptions? baseline = ValidateBaselineOptions(settings, detected.Package, document, out bool secretFromVariable);

        string? currentEnvironment = await ResolveCurrentEnvironmentAsync(settings, detected.Package, environments, document, interactive, cancellationToken);

        byte[] original = document.ToBytes();
        InitOutcome outcome = writer.Apply(document, new InitRequest(environments, currentEnvironment, projectId, settings.RotateSecret, baseline));
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

        if (secretFromVariable is false && IsBaselineVariableApplicable(settings))
        {
            outcome.Notes.Add(detected.Package == DeployPackage.OnPremises
                ? $"{BaselineSecretVariable} was ignored: it is only used with --baseline-url or a BaselineUrl configured in {writer.FileName}."
                : $"{BaselineSecretVariable} was ignored: it is not used with Umbraco Deploy Cloud.");
        }

        if (baseline is not null && BaselineSupport.IsSupported(detected.Version) is false)
        {
            outcome.Warnings.Add($"{detected.PackageId} {detected.Version} doesn't support Baseline connections (they need {BaselineSupport.Describe()}), so the Baseline settings are ignored.");
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
            BaselineUrl = outcome.BaselineUrl,
            BaselineSecretGenerated = outcome.GeneratedBaselineSecret is not null,
            ChildSecretGenerated = outcome.GeneratedChildSecret is not null,
            ChildSecretName = outcome.GeneratedChildSecretName,
            BaselineSecretConfigured = outcome.BaselineSecretConfigured,
            ChildSecretCount = outcome.ChildSecretCount,
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

        // Generated secrets are only written to the file, never to the output, so they can't end up in logs (such as CI/CD build logs)
        string written = result.DryRun ? "would be written" : "was written";
        if (outcome.GeneratedApiSecret is not null)
        {
            text.AppendLine();
            text.AppendLine($"The new ApiSecret {written} to Umbraco:Deploy:Settings:ApiSecret in {result.File}; every environment must be configured with this same value.");
        }

        if (outcome.GeneratedBaselineSecret is not null)
        {
            text.AppendLine();
            text.AppendLine($"The new Baseline secret {written} to Umbraco:Deploy:Settings:BaselineApiSecret in {result.File}; add the same value to ChildApiSecrets on the Baseline project's Live environment under a name of your choice, preferably as the environment variable {OnPremisesConfigurationWriter.ChildSecretVariable("<name>")}.");
        }

        if (outcome.GeneratedChildSecret is not null)
        {
            text.AppendLine();
            text.AppendLine($"The new child secret '{outcome.GeneratedChildSecretName}' {written} to Umbraco:Deploy:Settings:ChildApiSecrets in {result.File}; set the same value as BaselineApiSecret on the Child project.");
        }

        if (outcome.GeneratedApiSecret is not null || outcome.GeneratedBaselineSecret is not null || outcome.GeneratedChildSecret is not null)
        {
            text.AppendLine("Secrets are never shown in the output, so they can't end up in logs.");
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

        if (settings.RotateSecret)
        {
            throw new UsageException("--rotate-secret only applies to Umbraco Deploy On-Premises; Umbraco Cloud provisions the ApiSecret itself.");
        }

        string? projectId = settings.ProjectId ?? _environment.GetVariable(Cloud.CloudCredentialsResolver.ProjectIdVariable);
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        return Guid.TryParse(projectId, out Guid parsed) ? parsed : throw new UsageException($"The project ID '{projectId}' is not a GUID.");
    }

    private bool IsBaselineVariableApplicable(InitCommandSettings settings)
        => string.IsNullOrWhiteSpace(_environment.GetVariable(BaselineSecretVariable)) is false
            && settings.RotateBaselineSecret is false
            && settings.RemoveBaseline is false;

    private BaselineOptions? ValidateBaselineOptions(InitCommandSettings settings, DeployPackage package, JsonDocumentEditor document, out bool secretFromVariable)
    {
        secretFromVariable = false;
        var requested = new BaselineOptions(settings.BaselineUrl, Secret: null, settings.RotateBaselineSecret, settings.RemoveBaseline, settings.ChildSecret, settings.RemoveChildSecret);

        string? variableSecret = null;
        if (package == DeployPackage.OnPremises && IsBaselineVariableApplicable(settings))
        {
            bool hasBaselineUrl = settings.BaselineUrl is not null || OnPremisesConfigurationWriter.HasBaselineUrl(document);
            variableSecret = hasBaselineUrl ? _environment.GetVariable(BaselineSecretVariable) : null;
        }

        if (requested.IsEmpty && variableSecret is null)
        {
            return null;
        }

        if (package != DeployPackage.OnPremises)
        {
            throw new UsageException(
                "The Baseline options only apply to Umbraco Deploy On-Premises; Umbraco Cloud provisions the Baseline settings itself.",
                "Remove --baseline-url, --rotate-baseline-secret, --remove-baseline, --child-secret and --remove-child-secret.");
        }

        if (settings.RemoveBaseline && (settings.BaselineUrl is not null || settings.RotateBaselineSecret))
        {
            throw new UsageException("--remove-baseline cannot be combined with --baseline-url or --rotate-baseline-secret.");
        }

        string? url = null;
        if (settings.BaselineUrl is not null)
        {
            url = EnvironmentSpec.GetUrlError(settings.BaselineUrl) is string urlError
                ? throw new UsageException($"Invalid --baseline-url: {urlError}.", "Use the absolute HTTPS URL of the Baseline project's Live environment.")
                : settings.BaselineUrl.TrimEnd('/');

            // Umbraco Deploy ignores a Baseline URL with credentials, which would also end up in appsettings.json
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && string.IsNullOrEmpty(uri.UserInfo) is false)
            {
                throw new UsageException("Invalid --baseline-url: it must not contain a user name or password.", "Use the absolute HTTPS URL of the Baseline project's Live environment, without credentials.");
            }
        }

        string? secret = variableSecret;
        secretFromVariable = variableSecret is not null;
        if (secret is not null && ApiSecret.IsValid(secret) is false)
        {
            throw new UsageException(
                $"{BaselineSecretVariable} must be Base64 for at least 64 bytes.", "Generate one with --rotate-baseline-secret, or --child-secret on the Baseline project.");
        }

        string? childSecret = ValidateChildSecretName(settings.ChildSecret, "--child-secret");
        string? removeChildSecret = ValidateChildSecretName(settings.RemoveChildSecret, "--remove-child-secret");
        if (childSecret is not null && string.Equals(childSecret, removeChildSecret, StringComparison.OrdinalIgnoreCase))
        {
            throw new UsageException("--child-secret and --remove-child-secret cannot name the same child secret.");
        }

        return new BaselineOptions(url, secret, settings.RotateBaselineSecret, settings.RemoveBaseline, childSecret, removeChildSecret);
    }

    private static string? ValidateChildSecretName(string? name, string option)
    {
        if (name is null)
        {
            return null;
        }

        string trimmed = name.Trim();
        return trimmed.Length == 0 || trimmed.Contains(':', StringComparison.Ordinal)
            ? throw new UsageException($"Invalid {option} '{name}': the name must not be empty or contain ':'.")
            : trimmed;
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
