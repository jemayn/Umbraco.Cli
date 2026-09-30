using System.Text.Json.Serialization;

namespace Umbraco.Cli.Infrastructure.NuGet;

/// <summary>
/// Source-generated JSON metadata for the NuGet v3 API.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(NuGetProtocol.ServiceIndex))]
[JsonSerializable(typeof(NuGetProtocol.RegistrationIndex))]
[JsonSerializable(typeof(NuGetProtocol.RegistrationPage))]
internal sealed partial class NuGetJsonContext : JsonSerializerContext
{
}
