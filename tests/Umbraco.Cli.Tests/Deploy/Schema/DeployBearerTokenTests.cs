using System.Text;
using NUnit.Framework;
using Umbraco.Cli.Deploy.Schema;

namespace Umbraco.Cli.Tests.Deploy.Schema;

[TestFixture]
public class DeployBearerTokenTests
{
    // The example ApiSecret and ApiKey of TriggerDeploy.ps1; the expected tokens were computed with its Get-Signature and Get-AuthorizationHeader
    private const string ExampleApiSecret = "Wz0BhWer7lFSMJHTHfsE56WGh+N/imnmdPeV0XIkIF8YEuJs5PziNjAdIx47Rx3drgA6dPBV2A3ktIELrLubaQ==";
    private const string ExampleApiKey = "37D6E975DEF0F6EFED3681A28AA9C49BBF6FB68E";
    private const string Nonce = "8c327019-20bb-4b49-b514-386415648981";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1790000000);

    [Test]
    public void Create_WithApiSecret_MatchesTriggerDeployScript()
        => Assert.That(
            DeployBearerToken.Create(Convert.FromBase64String(ExampleApiSecret), new Uri("https://example.com/umbraco/umbracodeploy/extract/start/Umbraco%20CLI"), Now, Nonce),
            Is.EqualTo("Q2NONkJtWno0eGREc1o2YkdxYStqdHJvRUkzN3htMWFUWC9PM2I2dEYydz06OGMzMjcwMTktMjBiYi00YjQ5LWI1MTQtMzg2NDE1NjQ4OTgxOjE3OTAwMDAwMDA="));

    [Test]
    public void Create_WithApiKeyAndPathBase_MatchesTriggerDeployScript()
        => Assert.That(
            DeployBearerToken.Create(Encoding.UTF8.GetBytes(ExampleApiKey), new Uri("https://example.com/site/umbraco/umbracodeploy/statusreport/getcurrent"), Now, Nonce),
            Is.EqualTo("dGpCcE5jVTZXOHZIR0dQQmVPNnNKejVVakRneWhEVk91T0JGUlJLMllFaz06OGMzMjcwMTktMjBiYi00YjQ5LWI1MTQtMzg2NDE1NjQ4OTgxOjE3OTAwMDAwMDA="));

    [Test]
    public void Create_CollapsesDoubleSlashesLikeUmbracoDeploy()
    {
        byte[] key = Convert.FromBase64String(ExampleApiSecret);

        Assert.That(
            DeployBearerToken.Create(key, new Uri("https://example.com//site//umbraco/umbracodeploy/statusreport/getcurrent"), Now, Nonce),
            Is.EqualTo(DeployBearerToken.Create(key, new Uri("https://example.com/site/umbraco/umbracodeploy/statusreport/getcurrent"), Now, Nonce)));
    }
}
