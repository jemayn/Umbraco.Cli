namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// An uploaded Artifact, as returned by <c>POST /v2/projects/{projectId}/deployments/artifacts</c>.
/// </summary>
internal sealed record ArtifactResponse
{
    public string ArtifactId { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? BlobUrl { get; set; }

    public long FileSize { get; set; }

    public DateTimeOffset? CreatedUtc { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public string? DetectedDeploymentType { get; set; }
}
