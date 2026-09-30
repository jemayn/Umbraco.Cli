namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Checks a repository's <c>.gitignore</c> for the entries the Cloud scaffold uses to keep generated and environment-specific files out of git.
/// </summary>
internal static class GitignoreHints
{
    /// <summary>
    /// The entries whose absence causes problems on Umbraco Cloud (media, logs, temporary data and Umbraco Deploy marker files).
    /// </summary>
    public static readonly IReadOnlyList<string> ExpectedEntries = ["**/wwwroot/media", "**/umbraco/Logs/", "**/umbraco/Data/TEMP/", "**/umbraco/Deploy/deploy*"];

    /// <summary>
    /// Gets the expected entries missing from the <c>.gitignore</c> at the repository root (all of them when there is no <c>.gitignore</c>).
    /// </summary>
    public static IReadOnlyList<string> GetMissing(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, ".gitignore");
        if (File.Exists(path) is false)
        {
            return ExpectedEntries;
        }

        HashSet<string> lines = File.ReadAllLines(path).Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal);
        return ExpectedEntries.Where(entry => lines.Contains(entry) is false).ToList();
    }
}
