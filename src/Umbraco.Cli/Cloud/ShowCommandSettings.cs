namespace Umbraco.Cli.Cloud;

/// <summary>
/// The options of <c>umbraco cloud deployments show</c>.
/// </summary>
internal sealed record ShowCommandSettings(string? ProjectId, string DeploymentId, bool Wait, TimeSpan Timeout, string? Config = null);
