using NUnit.Framework;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class EnvironmentSpecTests
{
    [Test]
    public void Parse_AllParts_ReturnsSpec()
    {
        var spec = EnvironmentSpec.Parse("Name=Live; Type=LIVE; Url=https://example.com/; Id=efef5e89-a19b-434b-b68a-26e022a0ad52");

        Assert.That(spec, Is.EqualTo(new EnvironmentSpec("Live", "live", "https://example.com", Guid.Parse("efef5e89-a19b-434b-b68a-26e022a0ad52"))));
    }

    [Test]
    public void Parse_KeysAreCaseInsensitiveAndIdIsOptional()
    {
        var spec = EnvironmentSpec.Parse("name=Dev;type=development;url=https://localhost:44307");

        Assert.That(spec, Is.EqualTo(new EnvironmentSpec("Dev", "development", "https://localhost:44307", null)));
    }

    [Test]
    public void Parse_HttpLocalhost_IsAllowed()
        => Assert.That(EnvironmentSpec.Parse("Name=Local;Type=development;Url=http://localhost:5000").Url, Is.EqualTo("http://localhost:5000"));

    [TestCase("Type=live;Url=https://example.com", "Name is missing")]
    [TestCase("Name=Live;Url=https://example.com", "Type is missing")]
    [TestCase("Name=Live;Type=live", "Url is missing")]
    [TestCase("Name=Live;Type=live;Url=example.com", "is not an absolute URL")]
    [TestCase("Name=Live;Type=live;Url=http://example.com", "must use HTTPS")]
    [TestCase("Name=Live;Type=live;Url=https://example.com;Id=nope", "is not a GUID")]
    [TestCase("Name=Live;Type=live;Url=https://example.com;Colour=blue", "'Colour' is not one of")]
    [TestCase("Live", "is not a key=value pair")]
    public void Parse_Invalid_ThrowsUsageException(string value, string reason)
    {
        UsageException exception = Assert.Throws<UsageException>(() => EnvironmentSpec.Parse(value))!;

        Assert.That(exception.Message, Does.Contain(reason));
        Assert.That(exception.Hint, Does.Contain(EnvironmentSpec.Format));
    }
}
