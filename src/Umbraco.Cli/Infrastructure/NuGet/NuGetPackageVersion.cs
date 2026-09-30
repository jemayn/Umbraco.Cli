using NuGet.Versioning;

namespace Umbraco.Cli.Infrastructure.NuGet;

/// <summary>
/// A version of a NuGet package, as listed in the registration API.
/// </summary>
internal sealed record NuGetPackageVersion(string Id, NuGetVersion Version, bool Listed, DateTimeOffset? Published, IReadOnlyList<NuGetDependencyGroup> DependencyGroups)
{
    /// <summary>
    /// Gets the dependencies for <c>net10.0</c>: the group of that framework, else of the newest older .NET framework, else a framework-independent group.
    /// </summary>
    public IReadOnlyList<NuGetDependency> GetDependencies()
    {
        NuGetDependencyGroup? group = DependencyGroups
            .Select(candidate => (Group: candidate, Rank: Rank(candidate.TargetFramework)))
            .Where(candidate => candidate.Rank >= 0)
            .OrderByDescending(candidate => candidate.Rank)
            .Select(candidate => candidate.Group)
            .FirstOrDefault();

        return group?.Dependencies ?? [];
    }

    /// <summary>
    /// Ranks how well a target framework fits <c>net10.0</c> (higher is better, negative is incompatible).
    /// </summary>
    private static int Rank(string? targetFramework)
    {
        if (string.IsNullOrEmpty(targetFramework) || targetFramework.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string framework = targetFramework.ToLowerInvariant();
        if (framework.StartsWith(".netstandard", StringComparison.Ordinal) || framework.StartsWith("netstandard", StringComparison.Ordinal))
        {
            return 1;
        }

        // ".NETCoreApp3.1", "net8.0", "net10.0" (the registration API uses the long form, e.g. "net10.0" or ".NETCoreApp10.0")
        string version = framework.Replace(".netcoreapp", string.Empty, StringComparison.Ordinal).Replace("net", string.Empty, StringComparison.Ordinal);
        if (System.Version.TryParse(version, out System.Version? parsed) && parsed.Major >= 5 && parsed.Major <= 10)
        {
            return 10 + parsed.Major;
        }

        return -1;
    }
}
