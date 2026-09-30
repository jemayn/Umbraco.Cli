using NuGet.Versioning;
using Umbraco.Cli.Infrastructure.NuGet;

namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// An in-memory nuget.org.
/// </summary>
internal sealed class FakeNuGetClient : INuGetClient
{
    private readonly Dictionary<string, List<NuGetPackageVersion>> _packages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a package version with <c>net10.0</c> dependencies given as <c>"Id Range"</c>, e.g. <c>"Umbraco.Cms.Core [17.0.0, 18.0.0)"</c>.
    /// </summary>
    public FakeNuGetClient Add(string id, string version, DateTimeOffset? published = null, bool listed = true, params string[] dependencies)
    {
        var parsed = dependencies
            .Select(dependency => dependency.Split(' ', 2))
            .Select(parts => new NuGetDependency(parts[0], VersionRange.Parse(parts[1])))
            .ToList();

        if (_packages.TryGetValue(id, out List<NuGetPackageVersion>? versions) is false)
        {
            _packages[id] = versions = [];
        }

        versions.Add(new NuGetPackageVersion(id, NuGetVersion.Parse(version), listed, published ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), [new NuGetDependencyGroup("net10.0", parsed)]));
        return this;
    }

    public Task<IReadOnlyList<NuGetPackageVersion>> GetVersionsAsync(string packageId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NuGetPackageVersion>>(
            _packages.TryGetValue(packageId, out List<NuGetPackageVersion>? versions) ? versions.OrderBy(version => version.Version).ToList() : []);
}
