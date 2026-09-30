using System.Text.Json.Serialization;

namespace Umbraco.Cli.Infrastructure.NuGet;

/// <summary>
/// The shapes of the NuGet v3 service index and registration API that the CLI reads.
/// </summary>
internal static class NuGetProtocol
{
    internal sealed class ServiceIndex
    {
        public List<ServiceResource> Resources { get; set; } = [];
    }

    internal sealed class ServiceResource
    {
        [JsonPropertyName("@id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("@type")]
        public string Type { get; set; } = string.Empty;
    }

    internal sealed class RegistrationIndex
    {
        public List<RegistrationPage> Items { get; set; } = [];
    }

    internal sealed class RegistrationPage
    {
        [JsonPropertyName("@id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the leaves; <c>null</c> when the page must be fetched separately.
        /// </summary>
        public List<RegistrationLeaf>? Items { get; set; }
    }

    internal sealed class RegistrationLeaf
    {
        public CatalogEntry CatalogEntry { get; set; } = new();
    }

    internal sealed class CatalogEntry
    {
        public string Version { get; set; } = string.Empty;

        public bool? Listed { get; set; }

        public DateTimeOffset? Published { get; set; }

        public List<DependencyGroup>? DependencyGroups { get; set; }
    }

    internal sealed class DependencyGroup
    {
        public string? TargetFramework { get; set; }

        public List<Dependency>? Dependencies { get; set; }
    }

    internal sealed class Dependency
    {
        public string Id { get; set; } = string.Empty;

        public string? Range { get; set; }
    }
}
