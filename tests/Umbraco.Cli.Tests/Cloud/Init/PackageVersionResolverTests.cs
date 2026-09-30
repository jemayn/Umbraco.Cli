using NuGet.Versioning;
using NUnit.Framework;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud.Init;

[TestFixture]
public class PackageVersionResolverTests
{
    [Test]
    public async Task ResolveAsync_PicksTheNewestVersionInTheCmsMajor()
    {
        var nuGet = new FakeNuGetClient()
            .Add("Umbraco.Deploy.Cloud", "16.0.6", dependencies: "Umbraco.Cms.Web.Website [16.0.0, 17.0.0)")
            .Add("Umbraco.Deploy.Cloud", "17.0.0", dependencies: "Umbraco.Cms.Web.Website [17.0.0, 18.0.0)")
            .Add("Umbraco.Deploy.Cloud", "17.3.1", dependencies: "Umbraco.Cms.Web.Website [17.0.0, 18.0.0)")
            .Add("Umbraco.Deploy.Cloud", "18.1.1", dependencies: "Umbraco.Cms.Web.Website [18.0.0, 19.0.0)");

        Assert.That(await Resolve(nuGet, "Umbraco.Deploy.Cloud", "17.7.0"), Is.EqualTo("17.3.1"));
    }

    [Test]
    public async Task ResolveAsync_SkipsVersionsNeedingANewerCmsMinorTransitively()
    {
        var nuGet = new FakeNuGetClient()
            .Add("Umbraco.Automate", "17.3.0", dependencies: "Umbraco.Automate.Core [17.3.0, )")
            .Add("Umbraco.Automate", "17.4.0", dependencies: "Umbraco.Automate.Core [17.4.0, )")
            .Add("Umbraco.Automate.Core", "17.3.0", dependencies: "Umbraco.Cms.Core [17.0.0, 18.0.0)")
            .Add("Umbraco.Automate.Core", "17.4.0", dependencies: "Umbraco.Cms.Core [17.6.0, 18.0.0)");

        Assert.That(await Resolve(nuGet, "Umbraco.Automate", "17.5.0"), Is.EqualTo("17.3.0"));
        Assert.That(await Resolve(nuGet, "Umbraco.Automate", "17.6.0"), Is.EqualTo("17.4.0"));
    }

    [Test]
    public async Task ResolveAsync_IgnoresNonUmbracoDependenciesAndUnlistedVersions()
    {
        var nuGet = new FakeNuGetClient()
            .Add("Umbraco.Forms", "17.5.2", listed: false, dependencies: "Umbraco.Cms.Core [17.0.0, 18.0.0)")
            .Add("Umbraco.Forms", "17.5.1", dependencies: "Rebus [8.0.0, )");

        Assert.That(await Resolve(nuGet, "Umbraco.Forms", "17.0.0"), Is.EqualTo("17.5.1"));
    }

    [Test]
    public async Task ResolveAsync_ConsidersPrereleasesOnlyForPrereleaseCms()
    {
        var nuGet = new FakeNuGetClient()
            .Add("Umbraco.Forms", "19.0.0-beta1");

        Assert.That(await Resolve(nuGet, "Umbraco.Forms", "19.0.0"), Is.Null);
        Assert.That(await Resolve(nuGet, "Umbraco.Forms", "19.0.0-beta1"), Is.EqualTo("19.0.0-beta1"));
    }

    [Test]
    public async Task ResolveAsync_UnsatisfiableUmbracoDependency_IsIncompatible()
    {
        var nuGet = new FakeNuGetClient()
            .Add("Umbraco.Forms.Deploy", "17.0.1", dependencies: "Umbraco.Deploy.Infrastructure [17.9.0, )");

        Assert.That(await Resolve(nuGet, "Umbraco.Forms.Deploy", "17.7.0"), Is.Null);
    }

    [Test]
    public async Task ResolveAsync_UnknownPackage_ReturnsNull()
        => Assert.That(await Resolve(new FakeNuGetClient(), "Umbraco.Nope", "17.7.0"), Is.Null);

    private static async Task<string?> Resolve(FakeNuGetClient nuGet, string id, string cmsVersion)
        => (await new PackageVersionResolver(nuGet).ResolveAsync(id, NuGetVersion.Parse(cmsVersion), CancellationToken.None))?.ToNormalizedString();
}
