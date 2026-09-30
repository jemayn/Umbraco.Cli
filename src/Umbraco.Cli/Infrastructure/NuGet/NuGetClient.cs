using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using NuGet.Versioning;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Infrastructure.NuGet;

/// <inheritdoc />
/// <remarks>
/// Uses the nuget.org service index to find the registration API (SemVer 2, gzip) and caches responses for the lifetime of the client (one command).
/// </remarks>
internal sealed class NuGetClient : INuGetClient
{
    public const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";

    private const string RegistrationsResourceType = "RegistrationsBaseUrl/3.6.0";

    private const int MaxAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly IReporter _reporter;
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<NuGetPackageVersion>>> _versions = new(StringComparer.OrdinalIgnoreCase);
    private Task<string>? _registrationsBaseUrl;

    public NuGetClient(HttpClient httpClient, TimeProvider timeProvider, IReporter reporter)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _reporter = reporter;
    }

    public Task<IReadOnlyList<NuGetPackageVersion>> GetVersionsAsync(string packageId, CancellationToken cancellationToken)
        => _versions.GetOrAdd(packageId, id => LoadVersionsAsync(id, cancellationToken));

    private static NuGetPackageVersion? ToPackageVersion(string packageId, NuGetProtocol.CatalogEntry entry)
    {
        if (NuGetVersion.TryParse(entry.Version, out NuGetVersion? version) is false)
        {
            return null;
        }

        var groups = (entry.DependencyGroups ?? [])
            .Select(group => new NuGetDependencyGroup(
                group.TargetFramework,
                (group.Dependencies ?? [])
                    .Select(dependency => new NuGetDependency(dependency.Id, VersionRange.TryParse(dependency.Range ?? string.Empty, out VersionRange? range) ? range : VersionRange.All))
                    .ToList()))
            .ToList();

        // Unlisted packages are published "1900-01-01"
        DateTimeOffset? published = entry.Published?.Year > 1900 ? entry.Published : null;
        return new NuGetPackageVersion(packageId, version, entry.Listed ?? true, published, groups);
    }

    private async Task<IReadOnlyList<NuGetPackageVersion>> LoadVersionsAsync(string packageId, CancellationToken cancellationToken)
    {
        string baseUrl = await (_registrationsBaseUrl ??= LoadRegistrationsBaseUrlAsync(cancellationToken));
        NuGetProtocol.RegistrationIndex? index = await GetAsync(
            $"{baseUrl}{packageId.ToLowerInvariant()}/index.json",
            NuGetJsonContext.Default.RegistrationIndex,
            cancellationToken);

        if (index is null)
        {
            return [];
        }

        var versions = new List<NuGetPackageVersion>();
        foreach (NuGetProtocol.RegistrationPage page in index.Items)
        {
            List<NuGetProtocol.RegistrationLeaf> leaves = page.Items
                ?? (await GetAsync(page.Id, NuGetJsonContext.Default.RegistrationPage, cancellationToken))?.Items
                ?? [];

            versions.AddRange(leaves.Select(leaf => ToPackageVersion(packageId, leaf.CatalogEntry)).OfType<NuGetPackageVersion>());
        }

        return versions.OrderBy(version => version.Version).ToList();
    }

    private async Task<string> LoadRegistrationsBaseUrlAsync(CancellationToken cancellationToken)
    {
        NuGetProtocol.ServiceIndex index = await GetAsync(ServiceIndexUrl, NuGetJsonContext.Default.ServiceIndex, cancellationToken)
            ?? throw new CliException("The nuget.org service index could not be read.");

        string url = index.Resources.FirstOrDefault(resource => resource.Type == RegistrationsResourceType)?.Id
            ?? throw new CliException($"The nuget.org service index has no {RegistrationsResourceType} resource.");

        return url.EndsWith('/') ? url : url + "/";
    }

    /// <summary>
    /// Gets JSON, retrying transient failures; returns <c>null</c> for 404.
    /// </summary>
    private async Task<T?> GetAsync<T>(string url, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
                _reporter.Verbose($"GET {url} responded {(int)response.StatusCode} {response.ReasonPhrase}");
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                bool transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (transient is false || attempt == MaxAttempts)
                {
                    if (response.IsSuccessStatusCode is false)
                    {
                        throw new CliException($"nuget.org responded {(int)response.StatusCode} {response.ReasonPhrase} for {url}.");
                    }

                    return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken);
                }
            }
            catch (HttpRequestException exception) when (attempt < MaxAttempts)
            {
                _reporter.Verbose($"GET {url} failed: {exception.Message}");
            }
            catch (HttpRequestException exception)
            {
                throw new CliException($"Could not reach nuget.org: {exception.Message}", ExitCodes.Error, innerException: exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), _timeProvider, cancellationToken);
        }
    }
}
