namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// The body of <c>POST /v2/projects/{projectId}/deployments</c>.
/// </summary>
internal sealed record StartDeploymentRequest
{
    public required string ArtifactId { get; init; }

    public required string TargetEnvironmentAlias { get; init; }

    public required string CommitMessage { get; init; }

    public bool NoBuildAndRestore { get; init; }

    public bool SkipVersionCheck { get; init; }

    public bool SkipPreserveUmbracoCloudJson { get; init; }

    public bool RunSchemaExtraction { get; init; } = true;
}
