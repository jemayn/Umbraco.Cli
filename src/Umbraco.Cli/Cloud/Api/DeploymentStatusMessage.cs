namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// A status message reported while a Deployment runs.
/// </summary>
internal sealed record DeploymentStatusMessage
{
    public string Message { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }
}
