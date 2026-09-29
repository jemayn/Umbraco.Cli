using NUnit.Framework;
using Umbraco.Cli.Deploy.Configuration;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class ApiSecretTests
{
    [Test]
    public void Generate_Returns64RandomBytesAsBase64()
    {
        string first = ApiSecret.Generate();
        string second = ApiSecret.Generate();

        Assert.That(first, Has.Length.EqualTo(88));
        Assert.That(Convert.FromBase64String(first), Has.Length.EqualTo(64));
        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(ApiSecret.IsValid(first), Is.True);
    }

    [Test]
    public void IsValid_LongerSecret_IsValid()
        => Assert.That(ApiSecret.IsValid(Convert.ToBase64String(new byte[128])), Is.True);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not base64!")]
    [TestCase("c2hvcnQ=")]
    public void IsValid_MissingShortOrNotBase64_IsInvalid(string? value)
        => Assert.That(ApiSecret.IsValid(value), Is.False);
}
