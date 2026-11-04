namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// The Umbraco Cloud CI/CD API (v2).
/// </summary>
internal interface ICloudApiClient
{
    /// <summary>
    /// Uploads a zip file as an Artifact (never retried).
    /// </summary>
    Task<ArtifactResponse> UploadArtifactAsync(
        CloudCredentials credentials,
        string zipPath,
        string? description,
        string? version,
        IProgress<long>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts a Deployment of an Artifact to an Environment (never retried, as a retry could queue a second Deployment).
    /// </summary>
    Task<DeploymentResponse> StartDeploymentAsync(CloudCredentials credentials, StartDeploymentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the status of a Deployment, retrying transient failures. When <c>lastModifiedUtc</c> is set, only status messages after this time are returned.
    /// </summary>
    Task<DeploymentResponse> GetDeploymentAsync(CloudCredentials credentials, string deploymentId, DateTimeOffset? lastModifiedUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the ID of the latest Deployment started through the API (to the Environment with <c>environmentAlias</c>, when set), or <c>null</c> when there's none.
    /// </summary>
    Task<string?> GetLatestDeploymentIdAsync(CloudCredentials credentials, string? environmentAlias, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the configuration values and secrets of the project for local development (provisional, see ADR 0010).
    /// </summary>
    /// <exception cref="CloudApiException">Umbraco Cloud doesn't provide them (yet), or rejected the request.</exception>
    Task<IReadOnlyDictionary<string, string>> GetLocalDevelopmentValuesAsync(CloudCredentials credentials, CancellationToken cancellationToken);
}
