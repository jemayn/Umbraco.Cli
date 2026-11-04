using System.Text.Json;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Writes the Umbraco Cloud settings of an Umbraco project: the <c>Identity</c> block of <c>umbraco-cloud.json</c> and the Cloud licences in <c>appsettings.json</c>.
/// </summary>
internal static class CloudProjectSettings
{
    public const string UmbracoCloudJsonFileName = "umbraco-cloud.json";

    public const string CloudLicense = "UMBRACO-CLOUD";

    /// <summary>
    /// Creates <c>umbraco-cloud.json</c> with an <c>Identity</c> block of placeholder values when missing; otherwise leaves it unchanged.
    /// </summary>
    /// <returns><c>placeholder</c> or <c>unchanged</c>.</returns>
    public static string EnsureUmbracoCloudJson(string projectDirectory, List<string> filesWritten)
    {
        string path = Path.Combine(projectDirectory, UmbracoCloudJsonFileName);
        if (File.Exists(path))
        {
            return "unchanged";
        }

        JsonDocumentEditor document = JsonDocumentEditor.Parse("{}");
        document.Set(["Identity", "ClientId"], IdentityValues.PlaceholderClientId);
        document.Set(["Identity", "ClientSecret"], IdentityValues.PlaceholderClientSecret);
        AtomicFile.WriteAllBytes(path, document.ToBytes());
        filesWritten.Add(path);

        return "placeholder";
    }

    /// <summary>
    /// Sets <c>Umbraco:Licenses:Products:{key}</c> to <c>UMBRACO-CLOUD</c> in <c>appsettings.json</c> for each licence key without a value.
    /// </summary>
    public static void EnsureLicenses(string projectDirectory, IEnumerable<string> licenseKeys, List<string> filesWritten)
    {
        string path = Path.Combine(projectDirectory, "appsettings.json");
        JsonDocumentEditor document = File.Exists(path) ? Load(path) : JsonDocumentEditor.Parse("{}");
        bool changed = false;
        foreach (string key in licenseKeys)
        {
            string[] licensePath = ["Umbraco", "Licenses", "Products", key];
            if (string.IsNullOrEmpty(document.GetString(licensePath)))
            {
                document.Set(licensePath, CloudLicense);
                changed = true;
            }
        }

        if (changed)
        {
            AtomicFile.WriteAllBytes(path, document.ToBytes());
            filesWritten.Add(path);
        }
    }

    private static JsonDocumentEditor Load(string path)
    {
        try
        {
            return JsonDocumentEditor.Parse(File.ReadAllBytes(path));
        }
        catch (JsonException exception)
        {
            throw new CliException($"'{path}' is not a valid JSON object: {exception.Message}", ExitCodes.Error, innerException: exception);
        }
    }
}
