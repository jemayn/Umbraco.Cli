using System.Text.Json.Serialization;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Source-generated JSON metadata for the embedded Product catalogue.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
[JsonSerializable(typeof(ProductCatalog))]
internal sealed partial class InitJsonContext : JsonSerializerContext
{
}
