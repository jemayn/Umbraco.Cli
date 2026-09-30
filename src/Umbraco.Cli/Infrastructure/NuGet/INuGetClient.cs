namespace Umbraco.Cli.Infrastructure.NuGet;

/// <summary>
/// Reads package metadata from nuget.org.
/// </summary>
internal interface INuGetClient
{
    /// <summary>
    /// Gets all versions of a package (listed and unlisted), oldest first, or an empty list if the package doesn't exist.
    /// </summary>
    Task<IReadOnlyList<NuGetPackageVersion>> GetVersionsAsync(string packageId, CancellationToken cancellationToken);
}
