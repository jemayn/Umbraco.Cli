using NuGet.Versioning;

namespace Umbraco.Cli.Deploy;

/// <summary>
/// Which Umbraco Deploy On-Premises versions support Baseline connections.
/// </summary>
internal static class BaselineSupport
{
    private static readonly (int Major, int Minor)[] FirstSupported = [(17, 4), (18, 2)];

    /// <summary>
    /// Describes the first supported versions, for messages.
    /// </summary>
    public static string Describe() => string.Join(", or ", FirstSupported.Select(version => $"{version.Major}.{version.Minor}.0 or later"));

    /// <summary>
    /// Gets whether <paramref name="version" /> supports Baseline connections; an unparsable version is assumed to.
    /// </summary>
    public static bool IsSupported(string version)
    {
        if (NuGetVersion.TryParse(version, out NuGetVersion? parsed) is false)
        {
            return true;
        }

        // The first version of a release series counts, including its prereleases
        int highestMajor = FirstSupported[^1].Major;
        if (parsed.Major > highestMajor)
        {
            return true;
        }

        foreach ((int major, int minor) in FirstSupported)
        {
            if (parsed.Major == major)
            {
                return parsed.Minor >= minor;
            }
        }

        return false;
    }
}
