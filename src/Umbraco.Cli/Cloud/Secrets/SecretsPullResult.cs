namespace Umbraco.Cli.Cloud.Secrets;

/// <summary>
/// The result of <c>umbraco cloud secrets pull</c>, as written with <c>--output json</c>: the names of the values, never the values themselves.
/// </summary>
internal sealed record SecretsPullResult
{
    public required string File { get; init; }

    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>
    /// Gets a value indicating whether the file was added to the repository's <c>.gitignore</c>.
    /// </summary>
    public required bool GitignoreUpdated { get; init; }

    public required bool InGitRepository { get; init; }
}
