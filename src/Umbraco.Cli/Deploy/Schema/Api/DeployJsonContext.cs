using System.Text.Json.Serialization;
using Umbraco.Cli.Cloud.Api;

namespace Umbraco.Cli.Deploy.Schema.Api;

/// <summary>
/// Source-generated JSON metadata for Umbraco Deploy's Schema API, which uses PascalCase property names (keeps serialization trim- and AOT-compatible).
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ExtractStartResponse))]
[JsonSerializable(typeof(WorkStatusResponse))]
[JsonSerializable(typeof(ProblemDetailsResponse))]
internal sealed partial class DeployJsonContext : JsonSerializerContext
{
}
