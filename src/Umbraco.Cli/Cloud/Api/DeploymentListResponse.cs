namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// A page of Deployments, as returned by <c>GET /v2/projects/{projectId}/deployments</c> (newest first; only the IDs are used).
/// </summary>
internal sealed record DeploymentListResponse
{
    public IReadOnlyList<DeploymentListItem> Data { get; set; } = [];
}
