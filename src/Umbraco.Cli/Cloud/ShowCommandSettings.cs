namespace Umbraco.Cli.Cloud;

/// <summary>
/// The options of <c>umbraco cloud deployments show</c>: without a <see cref="DeploymentId" />, the latest deployment (to <see cref="Environment" />) is shown.
/// </summary>
internal sealed record ShowCommandSettings(string? ProjectId, string? DeploymentId, bool Wait, TimeSpan Timeout, string? Config = null, string? Environment = null);
