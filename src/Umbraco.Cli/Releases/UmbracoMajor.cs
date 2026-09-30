using System.Globalization;

namespace Umbraco.Cli.Releases;

/// <summary>
/// An Umbraco CMS major version and its support lifecycle (see ADR 0004).
/// </summary>
/// <param name="Major">The major version number.</param>
/// <param name="Released">When its first stable version was published, or <c>null</c> if only prereleases exist.</param>
internal sealed record UmbracoMajor(int Major, DateTimeOffset? Released)
{
    /// <summary>
    /// The first major the CLI supports, and the first major of the current LTS cadence.
    /// </summary>
    public const int FirstSupportedMajor = 17;

    /// <summary>
    /// Gets the release type: 17 and every fourth major after it are LTS.
    /// </summary>
    public ReleaseType ReleaseType => IsLts(Major) ? ReleaseType.Lts : ReleaseType.Sts;

    public DateTimeOffset? SupportFrom => Released?.AddMonths(6);

    public DateTimeOffset? SecurityFrom => Released?.AddMonths(ReleaseType == ReleaseType.Lts ? 24 : 9);

    public DateTimeOffset? EndOfLife => Released?.AddMonths(ReleaseType == ReleaseType.Lts ? 36 : 12);

    public static bool IsLts(int major) => major >= FirstSupportedMajor && (major - FirstSupportedMajor) % 4 == 0;

    public LifecyclePhase GetPhase(DateTimeOffset now)
    {
        if (Released is null)
        {
            return LifecyclePhase.Prerelease;
        }

        if (now >= EndOfLife)
        {
            return LifecyclePhase.EndOfLife;
        }

        if (now >= SecurityFrom)
        {
            return LifecyclePhase.Security;
        }

        return now >= SupportFrom ? LifecyclePhase.Support : LifecyclePhase.Active;
    }

    /// <summary>
    /// Describes the release type and lifecycle phase, e.g. "LTS, support until 27-11-2027".
    /// </summary>
    public string Describe(DateTimeOffset now)
    {
        string type = ReleaseType == ReleaseType.Lts ? "LTS" : "STS";
        return GetPhase(now) switch
        {
            LifecyclePhase.Prerelease => $"{type}, prerelease",
            LifecyclePhase.Active => $"{type}, active until {Format(SupportFrom)}",
            LifecyclePhase.Support => $"{type}, supported until {Format(SecurityFrom)}",
            LifecyclePhase.Security => $"{type}, security fixes only until {Format(EndOfLife)}",
            _ => $"{type}, end-of-life since {Format(EndOfLife)}",
        };
    }

    private static string Format(DateTimeOffset? date) => date?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "unknown";
}
