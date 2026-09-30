using NuGet.Versioning;

namespace Umbraco.Cli.Infrastructure.NuGet;

/// <summary>
/// A dependency of a package version on another package.
/// </summary>
internal sealed record NuGetDependency(string Id, VersionRange Range);
