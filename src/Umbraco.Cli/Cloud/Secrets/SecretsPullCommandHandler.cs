using System.Text;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud.Secrets;

/// <summary>
/// <c>umbraco cloud secrets pull</c>: pulls the configuration values and secrets for local development from Umbraco Cloud.
/// </summary>
internal sealed class SecretsPullCommandHandler
{
    private readonly IEnvironment _environment;
    private readonly LocalSecretsPuller _puller;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public SecretsPullCommandHandler(IEnvironment environment, LocalSecretsPuller puller, IOutput output, IReporter reporter)
    {
        _environment = environment;
        _puller = puller;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(string? projectId, CancellationToken cancellationToken)
    {
        CloudCredentials credentials = CloudCredentialsResolver.Resolve(projectId, _environment, uploadDirectory: _environment.CurrentDirectory);

        _reporter.Info("Pulling the local development values from Umbraco Cloud.");
        SecretsPullResult result = await _puller.PullAsync(credentials, _environment.CurrentDirectory, cancellationToken);
        if (result.InGitRepository is false)
        {
            _reporter.Warning($"'{_environment.CurrentDirectory}' isn't in a git repository, so {LocalEnvironmentFile.FileName} couldn't be added to a .gitignore; never commit it.");
        }

        _output.Write(result, CliJsonContext.Default.SecretsPullResult, _ => FormatText(result));
        return ExitCodes.Success;
    }

    private static string FormatText(SecretsPullResult result)
    {
        var text = new StringBuilder();
        text.AppendLine(result.Names.Count == 0
            ? $"Umbraco Cloud has no local development values for this project; wrote an empty {result.File}."
            : $"Wrote {result.Names.Count} local development value{(result.Names.Count == 1 ? string.Empty : "s")} to {result.File}: {string.Join(", ", result.Names)}.");
        if (result.GitignoreUpdated)
        {
            text.AppendLine($"  Added /{LocalEnvironmentFile.FileName} to .gitignore, so it isn't committed.");
        }

        text.Append("  The CLI uses these values in this repository; environment variables that are set take precedence.");
        return text.ToString();
    }
}
