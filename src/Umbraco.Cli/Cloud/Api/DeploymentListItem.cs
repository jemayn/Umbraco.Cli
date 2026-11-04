namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// A Deployment in a <see cref="DeploymentListResponse" />.
/// </summary>
internal sealed record DeploymentListItem
{
    public string Id { get; set; } = string.Empty;
}
