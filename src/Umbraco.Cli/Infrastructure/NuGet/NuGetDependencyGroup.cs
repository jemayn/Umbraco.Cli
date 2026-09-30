namespace Umbraco.Cli.Infrastructure.NuGet;

/// <summary>
/// The dependencies of a package version for one target framework.
/// </summary>
internal sealed record NuGetDependencyGroup(string? TargetFramework, IReadOnlyList<NuGetDependency> Dependencies);
