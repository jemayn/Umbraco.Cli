namespace Umbraco.Cli.Deploy.Schema.Api;

/// <summary>
/// The statuses of an Umbraco Deploy task: <c>New</c> and <c>Executing</c> while it runs, <c>Completed</c> when it succeeded, and
/// <c>Failed</c>, <c>Cancelled</c>, <c>TimedOut</c> or <c>Mismatch</c> when it failed. <c>Unknown</c> means the task wasn't found
/// (not started yet, or forgotten because the site restarted).
/// </summary>
internal static class WorkStatuses
{
    public const string Unknown = "Unknown";

    public const string New = "New";

    public const string Executing = "Executing";

    public const string Completed = "Completed";

    public static bool IsUnknown(string status) => string.Equals(status, Unknown, StringComparison.OrdinalIgnoreCase);

    public static bool IsRunning(string status)
        => string.Equals(status, New, StringComparison.OrdinalIgnoreCase) || string.Equals(status, Executing, StringComparison.OrdinalIgnoreCase);

    public static bool IsCompleted(string status) => string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);
}
