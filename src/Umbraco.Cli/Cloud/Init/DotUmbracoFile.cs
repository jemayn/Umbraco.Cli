using System.Text;
using System.Text.RegularExpressions;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The <c>.umbraco</c> file at the root of a Cloud project's repository, which tells Umbraco Cloud where the Umbraco project is.
/// </summary>
internal static partial class DotUmbracoFile
{
    public const string FileName = ".umbraco";

    /// <summary>
    /// Creates the TOML content, with <paramref name="baseDirectory" /> relative to the repository root (using forward slashes).
    /// </summary>
    public static string Create(string baseDirectory, string csprojFileName)
        => $"[project]\nbase = \"{baseDirectory.Replace('\\', '/')}\"\ncsproj = \"{csprojFileName}\"\n";

    /// <summary>
    /// Finds the root for a new <c>.umbraco</c> file: the git repository root, else the nearest directory with a solution file, else the project's directory.
    /// </summary>
    public static string FindRoot(string projectDirectory)
    {
        string? solutionDirectory = null;
        for (DirectoryInfo? directory = new(projectDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            if (solutionDirectory is null && (directory.EnumerateFiles("*.sln").Any() || directory.EnumerateFiles("*.slnx").Any()))
            {
                solutionDirectory = directory.FullName;
            }
        }

        return solutionDirectory ?? projectDirectory;
    }

    /// <summary>
    /// Finds an existing <c>.umbraco</c> file in the project's directory or one of its parents.
    /// </summary>
    public static string? FindExisting(string projectDirectory)
    {
        for (DirectoryInfo? directory = new(projectDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, FileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Ensures a <c>.umbraco</c> file exists for the Umbraco project: writes one if missing, and warns when an existing one points elsewhere.
    /// </summary>
    /// <returns>The path of the file written, or <c>null</c> when an existing file was kept.</returns>
    public static string? Ensure(string projectPath, Action<string> warn)
    {
        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        string? existing = FindExisting(projectDirectory);
        if (existing is not null)
        {
            string? pointsAt = Resolve(existing);
            if (pointsAt is null || string.Equals(Path.GetFullPath(pointsAt), Path.GetFullPath(projectPath), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) is false)
            {
                warn($"{existing} points at {pointsAt ?? "no project"}, not at {projectPath}; it was left unchanged.");
            }

            return null;
        }

        string root = FindRoot(projectDirectory);
        string relative = Path.GetRelativePath(root, projectDirectory);
        string path = Path.Combine(root, FileName);
        AtomicFile.WriteAllBytes(path, Encoding.UTF8.GetBytes(Create(relative == "." ? "." : relative, Path.GetFileName(projectPath))));
        return path;
    }

    /// <summary>
    /// Gets the full path of the project file an existing <c>.umbraco</c> file points at, or <c>null</c> if it can't be read.
    /// </summary>
    internal static string? Resolve(string dotUmbracoPath)
    {
        string content = File.ReadAllText(dotUmbracoPath);
        string? baseDirectory = Value(content, "base");
        string? csproj = Value(content, "csproj");
        return baseDirectory is null || csproj is null
            ? null
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dotUmbracoPath)!, baseDirectory, csproj));
    }

    private static string? Value(string content, string key)
    {
        Match match = KeyValuePattern().Matches(content).FirstOrDefault(candidate => candidate.Groups["key"].Value == key) ?? Match.Empty;
        return match.Success ? match.Groups["value"].Value : null;
    }

    [GeneratedRegex("""^\s*(?<key>[A-Za-z]+)\s*=\s*"(?<value>[^"]*)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex KeyValuePattern();
}
