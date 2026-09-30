using System.Text.Json;

namespace Umbraco.Cli.Infrastructure.Processes;

/// <summary>
/// The packages of a project, from the JSON output of <c>dotnet package list --include-transitive --format json</c>.
/// </summary>
internal sealed class PackageList
{
    private PackageList(IReadOnlyDictionary<string, string> topLevel, IReadOnlyDictionary<string, string> all)
    {
        TopLevel = topLevel;
        All = all;
    }

    /// <summary>
    /// Gets the directly referenced packages (ID to resolved version, case-insensitive).
    /// </summary>
    public IReadOnlyDictionary<string, string> TopLevel { get; }

    /// <summary>
    /// Gets the directly and transitively referenced packages (ID to resolved version, case-insensitive).
    /// </summary>
    public IReadOnlyDictionary<string, string> All { get; }

    public static PackageList Parse(string json)
    {
        var topLevel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using JsonDocument document = JsonDocument.Parse(json);
        foreach (JsonElement framework in Array(document.RootElement, "projects").SelectMany(project => Array(project, "frameworks")))
        {
            foreach ((string list, bool isTopLevel) in new[] { ("topLevelPackages", true), ("transitivePackages", false) })
            {
                foreach (JsonElement package in Array(framework, list))
                {
                    string? id = package.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
                    string version = package.TryGetProperty("resolvedVersion", out JsonElement versionElement) ? versionElement.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    all.TryAdd(id, version);
                    if (isTopLevel)
                    {
                        topLevel.TryAdd(id, version);
                    }
                }
            }
        }

        return new PackageList(topLevel, all);
    }

    private static IEnumerable<JsonElement> Array(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().ToList()
            : [];
}
