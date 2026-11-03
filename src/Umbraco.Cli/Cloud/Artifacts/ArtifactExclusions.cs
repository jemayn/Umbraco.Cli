namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// Files that are never deployed, also when git would commit them: git metadata, environment files that hold local secrets,
/// local SQLite databases, and Umbraco's logs and temporary files.
/// </summary>
/// <remarks>
/// A small deny-list to extend as needed. It doesn't have to be complete: Umbraco Cloud environments use their own
/// environment variables over deployed settings, and artifacts have a size limit.
/// </remarks>
internal static class ArtifactExclusions
{
    private static readonly string[] ExcludedDirectories = [".git/", "umbraco/Logs/", "umbraco/Data/TEMP/"];

    private static readonly string[] ExcludedExtensions = [".sqlite.db", ".sqlite.db-shm", ".sqlite.db-wal"];

    /// <summary>
    /// Checks whether a file must not be deployed.
    /// </summary>
    /// <param name="relativePath">The path relative to the deployed directory, with <c>/</c> separators.</param>
    public static bool IsExcluded(string relativePath)
    {
        string fileName = relativePath[(relativePath.LastIndexOf('/') + 1)..];

        // A .git file points to the git directory of a worktree or submodule
        if (fileName == ".git" || fileName == ".env" || fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ExcludedExtensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string directoryPath = "/" + relativePath;
        return ExcludedDirectories.Any(directory => directoryPath.Contains("/" + directory, StringComparison.OrdinalIgnoreCase));
    }
}
