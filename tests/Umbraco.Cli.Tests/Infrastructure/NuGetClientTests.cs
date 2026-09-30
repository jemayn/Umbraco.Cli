using System.Net;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.NuGet;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class NuGetClientTests
{
    private const string ServiceIndex = """
        { "resources": [
            { "@id": "https://api.test/search", "@type": "SearchQueryService" },
            { "@id": "https://api.test/registration/", "@type": "RegistrationsBaseUrl/3.6.0" } ] }
        """;

    private FakeHttpMessageHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = new FakeHttpMessageHandler();

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task GetVersionsAsync_ReadsInlinedAndPagedRegistrations()
    {
        _handler
            .Respond(HttpStatusCode.OK, ServiceIndex)
            .Respond(HttpStatusCode.OK, """
                { "items": [
                    { "@id": "https://api.test/registration/umbraco.forms/page1.json", "items": [
                        { "catalogEntry": { "version": "17.0.0", "published": "2025-11-27T10:00:00Z",
                            "dependencyGroups": [ { "targetFramework": "net10.0", "dependencies": [ { "id": "Umbraco.Forms.Core", "range": "[17.0.0, )" } ] } ] } },
                        { "catalogEntry": { "version": "17.0.1-rc", "listed": false, "published": "1900-01-01T00:00:00Z" } } ] },
                    { "@id": "https://api.test/registration/umbraco.forms/page2.json" } ] }
                """)
            .Respond(HttpStatusCode.OK, """
                { "@id": "https://api.test/registration/umbraco.forms/page2.json", "items": [
                    { "catalogEntry": { "version": "17.5.2", "published": "2026-09-01T10:00:00Z" } } ] }
                """);

        IReadOnlyList<NuGetPackageVersion> versions = await CreateClient().GetVersionsAsync("Umbraco.Forms", CancellationToken.None);

        Assert.That(versions.Select(version => version.Version.ToNormalizedString()), Is.EqualTo(new[] { "17.0.0", "17.0.1-rc", "17.5.2" }));
        Assert.That(versions[1].Listed, Is.False);
        Assert.That(versions[1].Published, Is.Null);
        Assert.That(versions[0].GetDependencies().Single().Id, Is.EqualTo("Umbraco.Forms.Core"));
        Assert.That(_handler.Requests.Select(request => request.Uri.ToString()), Is.EqualTo(new[]
        {
            NuGetClient.ServiceIndexUrl,
            "https://api.test/registration/umbraco.forms/index.json",
            "https://api.test/registration/umbraco.forms/page2.json",
        }));
    }

    [Test]
    public async Task GetVersionsAsync_CachesPerPackage()
    {
        _handler.Respond(HttpStatusCode.OK, ServiceIndex).Respond(HttpStatusCode.OK, """{ "items": [] }""");
        NuGetClient client = CreateClient();

        await client.GetVersionsAsync("Umbraco.Cms", CancellationToken.None);
        await client.GetVersionsAsync("umbraco.cms", CancellationToken.None);

        Assert.That(_handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetVersionsAsync_UnknownPackage_ReturnsEmpty()
    {
        _handler.Respond(HttpStatusCode.OK, ServiceIndex).Respond(HttpStatusCode.NotFound);

        Assert.That(await CreateClient().GetVersionsAsync("Umbraco.Nope", CancellationToken.None), Is.Empty);
    }

    [Test]
    public void GetVersionsAsync_ServerError_ThrowsAfterRetries()
    {
        var timeProvider = new FakeTimeProvider();
        _handler.Respond(HttpStatusCode.OK, ServiceIndex);
        for (int i = 0; i < 3; i++)
        {
            _handler.Respond(HttpStatusCode.ServiceUnavailable);
        }

        Assert.ThrowsAsync<CliException>(() => TimeAdvancer.RunAsync(
            CreateClient(timeProvider).GetVersionsAsync("Umbraco.Cms", CancellationToken.None),
            timeProvider,
            TimeSpan.FromSeconds(5)));
        Assert.That(_handler.Requests, Has.Count.EqualTo(4));
    }

    [Test]
    public void GetDependencies_PrefersNet10ThenOlderNetThenNetStandard()
    {
        NuGetDependency Dependency(string id) => new(id, NuGet.Versioning.VersionRange.All);
        NuGetDependencyGroup[] groups =
        [
            new NuGetDependencyGroup(".NETFramework4.7.2", [Dependency("Framework")]),
            new NuGetDependencyGroup(".NETStandard2.0", [Dependency("Standard")]),
            new NuGetDependencyGroup("net8.0", [Dependency("Net8")]),
        ];
        var version = new NuGetPackageVersion("P", NuGet.Versioning.NuGetVersion.Parse("1.0.0"), true, null, groups);

        Assert.That(version.GetDependencies().Single().Id, Is.EqualTo("Net8"));
    }

    private NuGetClient CreateClient(TimeProvider? timeProvider = null)
        => new(new HttpClient(_handler, disposeHandler: false), timeProvider ?? TimeProvider.System, Mock.Of<IReporter>());
}
