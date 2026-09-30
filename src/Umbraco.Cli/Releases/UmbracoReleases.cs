using NuGet.Versioning;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.NuGet;

namespace Umbraco.Cli.Releases;

/// <summary>
/// The Umbraco CMS releases on NuGet: majors with their lifecycle, and the Umbraco.Templates versions that create projects for them.
/// </summary>
internal sealed class UmbracoReleases
{
    public const string CmsPackageId = "Umbraco.Cms";

    public const string TemplatesPackageId = "Umbraco.Templates";

    private readonly INuGetClient _nuGetClient;

    public UmbracoReleases(INuGetClient nuGetClient) => _nuGetClient = nuGetClient;

    /// <summary>
    /// Gets the lifecycle information of a major, from the publish date of its first stable Umbraco.Cms version.
    /// </summary>
    public async Task<UmbracoMajor> GetMajorAsync(int major, CancellationToken cancellationToken)
    {
        IReadOnlyList<NuGetPackageVersion> versions = await _nuGetClient.GetVersionsAsync(CmsPackageId, cancellationToken);
        DateTimeOffset? released = versions
            .Where(version => version.Version.Major == major && version.Version.IsPrerelease is false && version.Published is not null)
            .OrderBy(version => version.Version)
            .FirstOrDefault()?.Published;

        return new UmbracoMajor(major, released);
    }

    /// <summary>
    /// Gets the listed Umbraco.Templates versions for supported majors, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<NuGetVersion>> GetTemplateVersionsAsync(CancellationToken cancellationToken)
        => (await _nuGetClient.GetVersionsAsync(TemplatesPackageId, cancellationToken))
            .Where(version => version.Listed && version.Version.Major >= UmbracoMajor.FirstSupportedMajor)
            .Select(version => version.Version)
            .ToList();

    /// <summary>
    /// Resolves a <c>--version</c> value (an alias, a major, a major.minor or an exact version) to an Umbraco.Templates version.
    /// </summary>
    /// <exception cref="UsageException">The value is invalid or matches no version.</exception>
    public static NuGetVersion Resolve(string value, IReadOnlyList<NuGetVersion> versions)
    {
        string alias = value.Trim().ToLowerInvariant();
        IEnumerable<NuGetVersion> stable = versions.Where(version => version.IsPrerelease is false);
        IEnumerable<NuGetVersion> prerelease = versions.Where(version => version.IsPrerelease);

        NuGetVersion? resolved = alias switch
        {
            "latest" => stable.Max(),
            "lts" => stable.Where(version => UmbracoMajor.IsLts(version.Major)).Max(),
            "rc" or "beta" => prerelease.Where(version => version.ReleaseLabels.First().StartsWith(alias, StringComparison.OrdinalIgnoreCase)).Max(),
            "next" or "preview" => prerelease.Max(),
            _ => ResolveNumeric(alias, versions, stable),
        };

        return resolved ?? throw new UsageException(
            $"No Umbraco version matches '{value}'.",
            "Use latest, lts, rc, beta, next, a major (17), a major.minor (17.2) or an exact version (17.2.0).");
    }

    private static NuGetVersion? ResolveNumeric(string value, IReadOnlyList<NuGetVersion> versions, IEnumerable<NuGetVersion> stable)
    {
        string[] parts = value.Split('.');
        if (parts.Length == 1 && int.TryParse(parts[0], out int major))
        {
            RequireSupported(major, value);
            return stable.Where(version => version.Major == major).Max();
        }

        if (parts.Length == 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out int minor))
        {
            RequireSupported(major, value);
            return stable.Where(version => version.Major == major && version.Minor == minor).Max();
        }

        if (NuGetVersion.TryParse(value, out NuGetVersion? exact))
        {
            RequireSupported(exact.Major, value);
            return versions.FirstOrDefault(version => version == exact);
        }

        throw new UsageException(
            $"'{value}' is not a valid Umbraco version.",
            "Use latest, lts, rc, beta, next, a major (17), a major.minor (17.2) or an exact version (17.2.0).");
    }

    private static void RequireSupported(int major, string value)
    {
        if (major < UmbracoMajor.FirstSupportedMajor)
        {
            throw new UsageException($"Umbraco {value} isn't supported: umbraco cloud init supports Umbraco {UmbracoMajor.FirstSupportedMajor} and later.");
        }
    }
}
