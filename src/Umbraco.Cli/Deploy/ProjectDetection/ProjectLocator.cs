using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Deploy.ProjectDetection;

/// <summary>
/// Finds the site's project file.
/// </summary>
internal static class ProjectLocator
{
    /// <summary>
    /// Gets the full path of the project file: <c>project</c> itself, the single <c>.csproj</c> in the <c>project</c> directory, or in the current directory when <c>project</c> is <c>null</c>.
    /// </summary>
    public static string Locate(string? project, string currentDirectory)
    {
        string path = Path.GetFullPath(project ?? currentDirectory, currentDirectory);
        if (File.Exists(path))
        {
            return string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase)
                ? path
                : throw new UsageException($"'{project}' is not a .csproj file.");
        }

        if (Directory.Exists(path) is false)
        {
            throw new UsageException($"'{project}' does not exist.");
        }

        string[] candidates = Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly);
        Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new UsageException($"No project file was found in '{path}'.", "Run the command in the site's project directory, or use --project."),
            _ => throw new UsageException(
                $"Several project files were found in '{path}': {string.Join(", ", candidates.Select(Path.GetFileName))}.",
                "Use --project to choose one."),
        };
    }
}
