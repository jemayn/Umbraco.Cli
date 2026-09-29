using System.Text.Json.Nodes;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// Reads and builds the <c>Workspaces</c> configuration of Umbraco Deploy.
/// </summary>
internal static class WorkspaceList
{
    public static IReadOnlyList<WorkspaceEntry> Read(JsonNode? workspaces)
    {
        var entries = new List<WorkspaceEntry>();
        if (workspaces is not JsonArray array)
        {
            return entries;
        }

        foreach (JsonObject item in array.OfType<JsonObject>())
        {
            string? name = GetString(item, "Name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            Guid.TryParse(GetString(item, "Id"), out Guid id);
            entries.Add(new WorkspaceEntry(id, name, GetString(item, "Type") ?? string.Empty, GetString(item, "Url") ?? string.Empty));
        }

        return entries;
    }

    /// <summary>
    /// Builds the new list from the given Environments, reusing the ID of an existing entry with the same name.
    /// </summary>
    public static IReadOnlyList<WorkspaceEntry> Build(IReadOnlyList<WorkspaceEntry> existing, IReadOnlyList<EnvironmentSpec> environments, Func<Guid> newId)
        => environments
            .Select(environment =>
            {
                Guid id = environment.Id
                    ?? existing.LastOrDefault(entry => entry.Id != Guid.Empty && string.Equals(entry.Name, environment.Name, StringComparison.OrdinalIgnoreCase))?.Id
                    ?? newId();

                return new WorkspaceEntry(id, environment.Name, environment.Type, environment.Url);
            })
            .ToList();

    public static JsonArray ToJson(IReadOnlyList<WorkspaceEntry> entries)
    {
        var array = new JsonArray();
        foreach (WorkspaceEntry entry in entries)
        {
            array.Add((JsonNode)new JsonObject
            {
                ["Id"] = entry.Id.ToString(),
                ["Name"] = entry.Name,
                ["Type"] = entry.Type,
                ["Url"] = entry.Url,
            });
        }

        return array;
    }

    private static string? GetString(JsonObject item, string name)
    {
        foreach (KeyValuePair<string, JsonNode?> property in item)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase) && property.Value is JsonValue value && value.TryGetValue(out string? text))
            {
                return text;
            }
        }

        return null;
    }
}
