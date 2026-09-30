using NuGet.Versioning;
using Umbraco.Cli.Infrastructure.NuGet;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Finds the newest version of an Umbraco package that is compatible with an Umbraco CMS version.
/// </summary>
/// <remarks>
/// Umbraco packages use the CMS major as their own major, but their CMS constraints are often only transitive and some need a newer minor,
/// so the <c>Umbraco.*</c> dependency graph is checked (other dependencies don't constrain the CMS version).
/// </remarks>
internal sealed class PackageVersionResolver
{
    private readonly INuGetClient _nuGetClient;
    private readonly Dictionary<(string Id, NuGetVersion Version, NuGetVersion CmsVersion), bool> _compatible = [];

    public PackageVersionResolver(INuGetClient nuGetClient) => _nuGetClient = nuGetClient;

    /// <summary>
    /// Gets the newest listed version in the CMS major whose <c>Umbraco.*</c> dependencies accept <paramref name="cmsVersion" />, or <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Prereleases are only considered when the CMS version itself is a prerelease.
    /// </remarks>
    public async Task<NuGetVersion?> ResolveAsync(string packageId, NuGetVersion cmsVersion, CancellationToken cancellationToken)
    {
        IReadOnlyList<NuGetPackageVersion> versions = await _nuGetClient.GetVersionsAsync(packageId, cancellationToken);
        IEnumerable<NuGetPackageVersion> candidates = versions
            .Where(version => version.Listed && version.Version.Major == cmsVersion.Major && (version.Version.IsPrerelease is false || cmsVersion.IsPrerelease))
            .OrderByDescending(version => version.Version);

        foreach (NuGetPackageVersion candidate in candidates)
        {
            if (await IsCompatibleAsync(candidate, cmsVersion, [], cancellationToken))
            {
                return candidate.Version;
            }
        }

        return null;
    }

    private static bool IsCms(string packageId)
        => packageId.Equals("Umbraco.Cms", StringComparison.OrdinalIgnoreCase) || packageId.StartsWith("Umbraco.Cms.", StringComparison.OrdinalIgnoreCase);

    private static bool IsUmbraco(string packageId) => packageId.StartsWith("Umbraco.", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsCompatibleAsync(NuGetPackageVersion package, NuGetVersion cmsVersion, HashSet<string> visiting, CancellationToken cancellationToken)
    {
        if (_compatible.TryGetValue((package.Id, package.Version, cmsVersion), out bool known))
        {
            return known;
        }

        // A dependency cycle can't add constraints beyond the ones already being checked
        if (visiting.Add(package.Id) is false)
        {
            return true;
        }

        bool compatible = true;
        foreach (NuGetDependency dependency in package.GetDependencies().Where(dependency => IsUmbraco(dependency.Id)))
        {
            if (IsCms(dependency.Id))
            {
                compatible = dependency.Range.Satisfies(cmsVersion);
            }
            else
            {
                // NuGet restores the lowest version satisfying the range, so that version's constraints apply (none satisfying it fails restore)
                IReadOnlyList<NuGetPackageVersion> versions = await _nuGetClient.GetVersionsAsync(dependency.Id, cancellationToken);
                NuGetPackageVersion? resolved = versions
                    .Where(version => version.Listed && dependency.Range.Satisfies(version.Version))
                    .OrderBy(version => version.Version)
                    .FirstOrDefault();

                compatible = resolved is not null && await IsCompatibleAsync(resolved, cmsVersion, visiting, cancellationToken);
            }

            if (compatible is false)
            {
                break;
            }
        }

        visiting.Remove(package.Id);
        _compatible[(package.Id, package.Version, cmsVersion)] = compatible;
        return compatible;
    }
}
