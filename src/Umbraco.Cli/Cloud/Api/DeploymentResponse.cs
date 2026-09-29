using System.Text.Json.Serialization;

namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// A Deployment, as returned when starting it and when getting its status.
/// </summary>
/// <remarks>
/// Response types use settable properties: the source-generated serializer sets missing init-only properties to <c>null</c> instead of keeping their defaults.
/// </remarks>
internal sealed record DeploymentResponse
{
    /// <summary>
    /// Gets the Artifact that was uploaded for this Deployment (only set by the CLI, when it uploaded the Artifact itself).
    /// </summary>
    [JsonPropertyOrder(-1)]
    public string? ArtifactId { get; set; }

    public string DeploymentId { get; set; } = string.Empty;

    public string? ProjectId { get; set; }

    public string? EnvironmentAlias { get; set; }

    public string DeploymentState { get; set; } = string.Empty;

    public IReadOnlyList<DeploymentStatusMessage> DeploymentStatusMessages { get; set; } = [];

    public DateTimeOffset? CreatedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public bool CloudRepositoryUpdated { get; set; }

    [JsonIgnore]
    public bool IsCompleted => DeploymentStates.IsCompleted(DeploymentState);

    [JsonIgnore]
    public bool IsFailed => DeploymentStates.IsFailed(DeploymentState);

    [JsonIgnore]
    public bool IsTerminal => IsCompleted || IsFailed;
}
