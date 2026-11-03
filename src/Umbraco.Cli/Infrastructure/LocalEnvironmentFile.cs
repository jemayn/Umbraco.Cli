using System.Text;
using System.Text.RegularExpressions;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The <c>.env.umbraco</c> file at the root of a repository: environment variables for local development, such as the values pulled from
/// Umbraco Cloud with <c>umbraco cloud secrets pull</c>, kept out of git (see ADR 0010).
/// </summary>
/// <remarks>
/// Lines are <c>NAME=value</c>, without quotes or escapes; blank lines and lines starting with <c>#</c> are ignored.
/// </remarks>
internal static partial class LocalEnvironmentFile
{
    public const string FileName = ".env.umbraco";

    /// <summary>
    /// Tells the user how to get the values that can be pulled from Umbraco Cloud.
    /// </summary>
    public const string PullHint = "For local development of an Umbraco Cloud project, run 'umbraco cloud secrets pull' to fetch it from Umbraco Cloud.";

    /// <summary>
    /// Finds the file in <paramref name="directory" /> or the directories above it, up to the root of the git repository (the directory containing <c>.git</c>).
    /// </summary>
    public static string? Find(string directory)
    {
        for (DirectoryInfo? current = new(Path.GetFullPath(directory)); current is not null; current = current.Parent)
        {
            string path = Path.Combine(current.FullName, FileName);
            if (File.Exists(path))
            {
                return path;
            }

            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return null;
            }
        }

        return null;
    }

    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();
            int separator = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || separator <= 0)
            {
                continue;
            }

            values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..];
        }

        return values;
    }

    /// <summary>
    /// Writes the values, replacing the file.
    /// </summary>
    public static void Write(string path, IReadOnlyDictionary<string, string> values)
    {
        var text = new StringBuilder();
        text.Append("# Environment variables for local development, written by 'umbraco cloud secrets pull'.\n");
        text.Append("# They contain secrets: never commit this file (it's in .gitignore). Run the command again to update them.\n");
        foreach ((string name, string value) in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append(name).Append('=').Append(value).Append('\n');
        }

        AtomicFile.WriteAllBytes(path, Encoding.UTF8.GetBytes(text.ToString()));
    }

    /// <summary>
    /// Turns configuration keys into environment variable names (<c>:</c> becomes <c>__</c>, as .NET configuration reads them) and checks that the values fit on one line.
    /// </summary>
    /// <exception cref="CliException">A name or value can't be written to the file.</exception>
    public static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in values)
        {
            string name = key.Replace(":", "__", StringComparison.Ordinal);
            if (VariableName().IsMatch(name) is false)
            {
                throw new CliException($"Umbraco Cloud returned a value with the name '{key}', which isn't a valid environment variable name.");
            }

            // Never include the value in the message: it's a secret
            if (value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
            {
                throw new CliException($"Umbraco Cloud returned a value for {name} that spans several lines, which can't be written to {FileName}.");
            }

            normalized[name] = value;
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.-]*$")]
    private static partial Regex VariableName();
}
