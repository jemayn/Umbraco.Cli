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

    /// <summary>
    /// How deep <see cref="Discover" /> looks for an Umbraco project below the repository root.
    /// </summary>
    private const int MaxSearchDepth = 4;

    private static readonly string[] IdPath = ["Deploy", "Project", "Id"];

    private static readonly string[] SkippedDirectories = ["bin", "obj", "node_modules"];

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

    /// <summary>
    /// Finds the <c>umbraco-cloud.json</c> for a command run in <paramref name="currentDirectory" />: in that directory or (in a git repository) a parent, up to the root of the
    /// repository (directly, or in the project a <c>.umbraco</c> file points at), else in the single Umbraco project below the repository root (the repository
    /// of an Artifact deployment has no <c>.umbraco</c> file); <c>null</c> when there's none.
    /// </summary>
    /// <exception cref="UsageException">Several Umbraco projects with an <c>umbraco-cloud.json</c> were found below the repository root.</exception>
    public static string? Discover(string currentDirectory)
    {
        string start = Path.GetFullPath(currentDirectory);
        if (RepositoryRoot.Find(start) is not string root)
        {
            // Outside a repository, there's no boundary to search up to or below
            return FindFile(start);
        }

        for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
        {
            if (FindFile(directory.FullName) is string path)
            {
                return path;
            }

            if (directory.FullName.TrimEnd(Path.DirectorySeparatorChar) == root.TrimEnd(Path.DirectorySeparatorChar))
            {
                break;
            }
        }

        var projects = new List<string>();
        FindProjectFiles(root, MaxSearchDepth, projects);
        return projects.Count switch
        {
            0 => null,
            1 => projects[0],
            _ => throw new UsageException(
                $"Several Umbraco projects with an {FileName} were found: {string.Join(", ", projects.Select(path => Path.GetRelativePath(start, path)))}.",
                "Run the command in the directory of the Umbraco project."),
        };
    }

    /// <summary>
    /// Adds the <c>umbraco-cloud.json</c> files next to a project file (leaving out copies in published output) below <paramref name="directory" />.
    /// </summary>
    private static void FindProjectFiles(string directory, int depth, List<string> found)
    {
        string path = Path.Combine(directory, FileName);
        if (File.Exists(path) && Directory.EnumerateFiles(directory, "*.csproj").Any())
        {
            found.Add(path);
        }

        if (depth == 0)
        {
            return;
        }

        foreach (string child in Directory.EnumerateDirectories(directory, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            string name = Path.GetFileName(child);
            if (name.StartsWith('.') is false && SkippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase) is false)
            {
                FindProjectFiles(child, depth - 1, found);
            }
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
