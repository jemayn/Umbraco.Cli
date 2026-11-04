using System.Text.Json.Serialization;

namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// Source-generated JSON metadata for the Umbraco Cloud API (keeps serialization trim- and AOT-compatible).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ArtifactResponse))]
[JsonSerializable(typeof(StartDeploymentRequest))]
[JsonSerializable(typeof(DeploymentResponse))]
[JsonSerializable(typeof(DeploymentListResponse))]
[JsonSerializable(typeof(ProblemDetailsResponse))]
[JsonSerializable(typeof(LocalDevelopmentValuesResponse))]
internal sealed partial class CloudJsonContext : JsonSerializerContext
{
}
