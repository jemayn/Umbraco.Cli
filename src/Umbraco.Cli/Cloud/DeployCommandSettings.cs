using Umbraco.Cli.Cloud.Artifacts;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// The options of <c>umbraco cloud deployments deploy</c>.
/// </summary>
internal sealed record DeployCommandSettings
{
    public string? ProjectId { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the Artifact to deploy; when not set, <see cref="Source" /> is uploaded first.
    /// </summary>
    public string? ArtifactId { get; init; }

    public ArtifactSource? Source { get; init; }

    public required string Environment { get; init; }

    public string? Message { get; init; }

    public bool NoBuildAndRestore { get; init; }

    public bool SkipVersionCheck { get; init; }

    public bool SkipPreserveUmbracoCloudJson { get; init; }

    public bool NoSchemaExtraction { get; init; }

    public bool NoWait { get; init; }

    public TimeSpan Timeout { get; init; } = DeploymentWaiter.DefaultTimeout;
}
