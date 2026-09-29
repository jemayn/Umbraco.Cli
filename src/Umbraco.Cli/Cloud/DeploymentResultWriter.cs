using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Writes a Deployment as the command result and maps its state to the exit code.
/// </summary>
internal static class DeploymentResultWriter
{
    public static int Write(DeploymentResponse deployment, IOutput output, IReporter reporter)
    {
        output.Write(deployment, CliJsonContext.Default.DeploymentResponse, FormatText);
        if (deployment.IsFailed)
        {
            reporter.Error($"Deployment {deployment.DeploymentId} failed.", "See the status messages above, or run 'umbraco cloud deployments show' with the deployment ID.");
            return ExitCodes.DeploymentFailed;
        }

        return ExitCodes.Success;
    }

    private static string FormatText(DeploymentResponse deployment)
    {
        string text = $"Deployment {deployment.DeploymentId}";
        if (string.IsNullOrEmpty(deployment.ArtifactId) is false)
        {
            text += $" of artifact {deployment.ArtifactId}";
        }

        if (string.IsNullOrEmpty(deployment.EnvironmentAlias) is false)
        {
            text += $" to {deployment.EnvironmentAlias}";
        }

        text += $": {deployment.DeploymentState}";
        if (deployment.IsTerminal && deployment.CreatedUtc is DateTimeOffset created && deployment.CompletedUtc is DateTimeOffset completed && completed >= created)
        {
            text += $" in {Durations.Format(completed - created)}";
        }

        return text + ".";
    }
}
