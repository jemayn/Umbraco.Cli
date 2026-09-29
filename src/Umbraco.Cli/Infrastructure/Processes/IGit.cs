namespace Umbraco.Cli.Infrastructure.Processes;

/// <summary>
/// The git operations the CLI needs.
/// </summary>
internal interface IGit
{
    /// <summary>
    /// Lists the files under <paramref name="directory" /> that git would commit: tracked and untracked files, excluding ignored ones.
    /// </summary>
    /// <returns>Paths relative to <paramref name="directory" />, using forward slashes.</returns>
    /// <exception cref="CliException">git is not installed, or the directory is not in a git work tree.</exception>
    Task<IReadOnlyList<string>> ListFilesAsync(string directory, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the subject of the <c>HEAD</c> commit, or <c>null</c> if it can't be determined (no git, not a repository, no commits).
    /// </summary>
    Task<string?> GetHeadSubjectAsync(string directory, CancellationToken cancellationToken);
}
