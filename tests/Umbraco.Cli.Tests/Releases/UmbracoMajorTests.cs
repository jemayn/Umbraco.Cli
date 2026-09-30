using NUnit.Framework;
using Umbraco.Cli.Releases;

namespace Umbraco.Cli.Tests.Releases;

[TestFixture]
public class UmbracoMajorTests
{
    [TestCase(17, "Lts")]
    [TestCase(18, "Sts")]
    [TestCase(19, "Sts")]
    [TestCase(20, "Sts")]
    [TestCase(21, "Lts")]
    [TestCase(25, "Lts")]
    public void ReleaseType_Is17AndEveryFourthMajorLts(int major, string expected)
        => Assert.That(new UmbracoMajor(major, null).ReleaseType, Is.EqualTo(Enum.Parse<ReleaseType>(expected)));

    [Test]
    public void Sts_MatchesThePublishedLifecycleOf18()
    {
        // umbraco.com: released 25-06-2026, security phase from 25-03-2027, end-of-life 25-06-2027
        var major = new UmbracoMajor(18, new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.That(major.SupportFrom, Is.EqualTo(new DateTimeOffset(2026, 12, 25, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(major.SecurityFrom, Is.EqualTo(new DateTimeOffset(2027, 3, 25, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(major.EndOfLife, Is.EqualTo(new DateTimeOffset(2027, 6, 25, 0, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void Lts_MatchesThePublishedLifecycleOf17()
    {
        // umbraco.com: released 27-11-2025, security phase from 27-11-2027, end-of-life 27-11-2028
        var major = new UmbracoMajor(17, new DateTimeOffset(2025, 11, 27, 0, 0, 0, TimeSpan.Zero));

        Assert.That(major.SecurityFrom, Is.EqualTo(new DateTimeOffset(2027, 11, 27, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(major.EndOfLife, Is.EqualTo(new DateTimeOffset(2028, 11, 27, 0, 0, 0, TimeSpan.Zero)));
    }

    [TestCase("2026-06-25", "Active")]
    [TestCase("2026-12-24", "Active")]
    [TestCase("2026-12-25", "Support")]
    [TestCase("2027-03-25", "Security")]
    [TestCase("2027-06-24", "Security")]
    [TestCase("2027-06-25", "EndOfLife")]
    public void GetPhase_AtPhaseBoundaries(string now, string expected)
    {
        var major = new UmbracoMajor(18, new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.That(major.GetPhase(DateTimeOffset.Parse(now + "T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(Enum.Parse<LifecyclePhase>(expected)));
    }

    [Test]
    public void GetPhase_WithoutStableRelease_IsPrerelease()
        => Assert.That(new UmbracoMajor(19, null).GetPhase(DateTimeOffset.UtcNow), Is.EqualTo(LifecyclePhase.Prerelease));

    [Test]
    public void Describe_UsesDanishDateFormat()
    {
        var major = new UmbracoMajor(18, new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.That(major.Describe(new DateTimeOffset(2027, 4, 1, 0, 0, 0, TimeSpan.Zero)), Is.EqualTo("STS, security fixes only until 25-06-2027"));
    }
}
