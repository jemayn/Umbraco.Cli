using System.Text.Json.Serialization;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Deploy.Configuration;

namespace Umbraco.Cli;

/// <summary>
/// Source-generated JSON metadata for the results written with <c>--output json</c> (a public contract: don't rename properties).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(ArtifactResponse))]
[JsonSerializable(typeof(DeploymentResponse))]
[JsonSerializable(typeof(InitResult))]
[JsonSerializable(typeof(CloudInitResult))]
internal sealed partial class CliJsonContext : JsonSerializerContext
{
}
