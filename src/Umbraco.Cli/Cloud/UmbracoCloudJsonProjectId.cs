using System.Text.Json;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Reads the Cloud project ID (<c>Deploy:Project:Id</c>) from <c>umbraco-cloud.json</c>.
/// </summary>
internal static class UmbracoCloudJsonProjectId
{
    public const string FileName = "umbraco-cloud.json";

    private static readonly string[] IdPath = ["Deploy", "Project", "Id"];

    /// <summary>
    /// Gets the project ID from <paramref name="config" /> when it's a file, else from the <c>umbraco-cloud.json</c> at the root of that directory
    /// (published output) or in the project its <c>.umbraco</c> file points at (source code); <c>null</c> when there's none.
    /// </summary>
    /// <exception cref="UsageException"><paramref name="config" /> doesn't exist.</exception>
    public static string? Find(string config)
    {
        if (File.Exists(config) is false && Directory.Exists(config) is false)
        {
            throw new UsageException($"'{config}' doesn't exist.", $"Use --config with the {FileName} of the Umbraco project, or the folder containing it.");
        }

        string? path = File.Exists(config) ? config : FindFile(config);
        if (path is null)
        {
            return null;
        }

        try
        {
            return JsonDocumentEditor.Parse(File.ReadAllBytes(path)).GetString(IdPath) is { } id && string.IsNullOrWhiteSpace(id) is false ? id.Trim() : null;
        }
        catch (JsonException exception)
        {
            throw new CliException($"'{path}' is not a valid JSON object: {exception.Message}", ExitCodes.Error, innerException: exception);
        }
    }

    private static string? FindFile(string directory)
    {
        string published = Path.Combine(directory, FileName);
        if (File.Exists(published))
        {
            return published;
        }

        string dotUmbraco = Path.Combine(directory, DotUmbracoFile.FileName);
        if (File.Exists(dotUmbraco) && DotUmbracoFile.Resolve(dotUmbraco) is string projectPath)
        {
            string project = Path.Combine(Path.GetDirectoryName(projectPath)!, FileName);
            return File.Exists(project) ? project : null;
        }

        return null;
    }
}
