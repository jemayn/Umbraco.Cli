namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Finds the root of the git repository (the directory containing <c>.git</c>) without running git.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>
    /// Gets <paramref name="directory" /> or the nearest directory above it that contains <c>.git</c> (a directory, or a file in a worktree or submodule), or <c>null</c> outside a repository.
    /// </summary>
    public static string? Find(string directory)
    {
        for (DirectoryInfo? current = new(Path.GetFullPath(directory)); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
        }

        return null;
    }
}
