using NuGet.Versioning;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Releases;

namespace Umbraco.Cli.Tests.Releases;

[TestFixture]
public class UmbracoReleasesTests
{
    private static readonly NuGetVersion[] Versions = new[]
    {
        "17.0.0-beta", "17.0.0-rc1", "17.0.0", "17.2.0", "17.2.1", "17.7.0",
        "18.0.0-beta2", "18.0.0-rc", "18.0.0", "18.1.0", "18.2.0-rc", "18.2.0",
        "19.0.0-beta1",
    }.Select(NuGetVersion.Parse).ToArray();

    [TestCase("latest", "18.2.0")]
    [TestCase("LATEST", "18.2.0")]
    [TestCase("lts", "17.7.0")]
    [TestCase("rc", "18.2.0-rc")]
    [TestCase("beta", "19.0.0-beta1")]
    [TestCase("next", "19.0.0-beta1")]
    [TestCase("preview", "19.0.0-beta1")]
    [TestCase("17", "17.7.0")]
    [TestCase("17.2", "17.2.1")]
    [TestCase("17.2.0", "17.2.0")]
    [TestCase("18.0.0-rc", "18.0.0-rc")]
    public void Resolve_ReturnsTheMatchingVersion(string value, string expected)
        => Assert.That(UmbracoReleases.Resolve(value, Versions).ToNormalizedString(), Is.EqualTo(expected));

    [TestCase("sts")]
    [TestCase("16")]
    [TestCase("13.5")]
    [TestCase("16.5.1")]
    [TestCase("17.3")]
    [TestCase("17.9.9")]
    [TestCase("soon")]
    public void Resolve_InvalidOrUnavailable_ThrowsUsageException(string value)
        => Assert.Throws<UsageException>(() => UmbracoReleases.Resolve(value, Versions));
}
