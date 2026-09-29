namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// The terminal Deployment states (the others are Created, Pending, Queued and InProgress).
/// </summary>
internal static class DeploymentStates
{
    public const string Completed = "Completed";

    public const string Failed = "Failed";

    public static bool IsCompleted(string state) => string.Equals(state, Completed, StringComparison.OrdinalIgnoreCase);

    public static bool IsFailed(string state) => string.Equals(state, Failed, StringComparison.OrdinalIgnoreCase);
}
