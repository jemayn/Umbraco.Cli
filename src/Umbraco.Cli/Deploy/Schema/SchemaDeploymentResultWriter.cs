using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// Writes a schema deployment as the command result and maps its status to the exit code.
/// </summary>
internal static class SchemaDeploymentResultWriter
{
    public static int Write(SchemaDeploymentResult result, string targetName, IOutput output, IReporter reporter)
    {
        output.Write(result, CliJsonContext.Default.SchemaDeploymentResult, value => FormatText(value, targetName));
        if (result.Log is not null)
        {
            reporter.Verbose($"Umbraco Deploy log:{Environment.NewLine}{result.Log.TrimEnd()}");
        }

        if (result.IsFailed)
        {
            string detail = result.Exception?.Detail is { Length: > 0 } message
                ? message.Trim().TrimEnd('.')
                : result.Mismatches is { Count: > 0 } mismatches ? $"the schema of {mismatches.Count} item(s) doesn't match ({string.Join(", ", mismatches.Take(5))})" : result.Status;

            reporter.Error(
                $"Schema deployment {result.TaskId} to {targetName} failed: {detail}.",
                result.Log is null ? "Check the Umbraco Deploy log of the Environment." : "Run with --verbose to see the Umbraco Deploy log.");
            return ExitCodes.DeploymentFailed;
        }

        return ExitCodes.Success;
    }

    private static string FormatText(SchemaDeploymentResult result, string targetName)
        => result.IsStarted
            ? $"Schema deployment {result.TaskId} to {targetName} started."
            : $"Schema deployment {result.TaskId} to {targetName}: {result.Status}{(result.Restarted ? " (started again)" : string.Empty)}.";
}
