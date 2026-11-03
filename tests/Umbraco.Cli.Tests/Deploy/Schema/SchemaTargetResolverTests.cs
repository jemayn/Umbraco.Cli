using System.Text;
using NUnit.Framework;
using Umbraco.Cli.Deploy.Schema;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Deploy.Schema;

[TestFixture]
public class SchemaTargetResolverTests
{
    private const string CloudSecret = "Y2xvdWQtc2VjcmV0";
    private const string AppSettingsSecret = "YXBwc2V0dGluZ3Mtc2VjcmV0";

    private string _directory = null!;
    private FakeEnvironment _environment = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _environment = new FakeEnvironment(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Resolve_Url_UsesOptionsAndKeepsThePathBase()
    {
        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = CloudSecret;

        SchemaTarget target = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = "https://example.com/site/" }, _environment);

        Assert.That(target.BaseUrl, Is.EqualTo(new Uri("https://example.com/site/")));
        Assert.That(target.Url, Is.EqualTo("https://example.com/site"));
        Assert.That(target.Environment, Is.Null);
        Assert.That(target.DisplayName, Is.EqualTo("https://example.com/site"));
        Assert.That(Key(target), Is.EqualTo("cloud-secret"));
        Assert.That(target.Credentials.Source, Is.EqualTo(SchemaTargetResolver.ApiSecretVariable));
    }

    [TestCase("http://example.com")]
    [TestCase("example.com")]
    public void Resolve_InvalidUrl_ThrowsUsageException(string url)
        => Assert.Throws<UsageException>(() => SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = url }, new FakeEnvironment(_directory) { Variables = { [SchemaTargetResolver.ApiSecretVariable] = CloudSecret } }));

    [Test]
    public void Resolve_Environment_UsesTheWorkspacesOfUmbracoCloudJsonFirst()
    {
        WriteCloudJson();
        WriteAppSettings();

        SchemaTarget target = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "live" }, _environment);

        Assert.That(target.BaseUrl, Is.EqualTo(new Uri("https://live.example.com/")));
        Assert.That(target.Environment, Is.EqualTo("live"));
        Assert.That(Key(target), Is.EqualTo("cloud-secret"));
        Assert.That(target.Credentials.Source, Is.EqualTo("umbraco-cloud.json"));
    }

    [Test]
    public void Resolve_Environment_FallsBackToAppSettings()
    {
        WriteAppSettings();

        SchemaTarget target = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Staging", Config = _directory }, new FakeEnvironment(Path.GetTempPath()));

        Assert.That(target.BaseUrl, Is.EqualTo(new Uri("https://staging.example.com/")));
        Assert.That(Key(target), Is.EqualTo("appsettings-secret"));
        Assert.That(target.Credentials.Source, Is.EqualTo("appsettings.json"));
    }

    [Test]
    public void Resolve_ConfigFile_ReadsOnlyThatFile()
    {
        WriteCloudJson();
        WriteAppSettings();
        File.Copy(Path.Combine(_directory, "appsettings.json"), Path.Combine(_directory, "appsettings.Production.json"));

        SchemaTarget fromCloud = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Live", Config = "umbraco-cloud.json" }, _environment);
        SchemaTarget fromAppSettings = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Live", Config = "appsettings.Production.json" }, _environment);

        Assert.That(fromCloud.BaseUrl, Is.EqualTo(new Uri("https://live.example.com/")));
        Assert.That(fromAppSettings.BaseUrl, Is.EqualTo(new Uri("https://other-live.example.com/")));
        Assert.That(Key(fromAppSettings), Is.EqualTo("appsettings-secret"));
        Assert.Throws<UsageException>(() => SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Staging", Config = "umbraco-cloud.json" }, _environment));
    }

    [Test]
    public void Resolve_UnknownEnvironment_ListsTheConfiguredOnes()
    {
        WriteCloudJson();
        WriteAppSettings();

        UsageException exception = Assert.Throws<UsageException>(() => SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Test" }, _environment))!;

        Assert.That(exception.Message, Does.Contain("'Test'").And.Contain("Development, Live, Staging"));
    }

    [Test]
    public void Resolve_PrefersEnvironmentVariablesOverFiles()
    {
        WriteCloudJson();
        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = AppSettingsSecret;

        SchemaTarget fromSecretVariable = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Live" }, _environment);
        _environment.Variables[SchemaTargetResolver.ApiKeyVariable] = "my-key";
        SchemaTarget fromKeyVariable = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Environment = "Live" }, _environment);

        Assert.That(Key(fromSecretVariable), Is.EqualTo("appsettings-secret"));
        Assert.That(fromSecretVariable.Credentials.Source, Is.EqualTo(SchemaTargetResolver.ApiSecretVariable));
        Assert.That(Key(fromKeyVariable), Is.EqualTo("my-key"));
        Assert.That(fromKeyVariable.Credentials.Source, Is.EqualTo(SchemaTargetResolver.ApiKeyVariable));
    }

    [Test]
    public void Resolve_ApiKeyWinsOverApiSecretInTheSameFile()
    {
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), """
            {
              // Comments are allowed, as in .NET configuration
              "Umbraco": { "Deploy": { "Settings": { "ApiKey": "legacy-key", "ApiSecret": "YXBwc2V0dGluZ3Mtc2VjcmV0" } } }
            }
            """);

        SchemaTarget target = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = "https://example.com" }, _environment);

        Assert.That(Key(target), Is.EqualTo("legacy-key"));
    }

    [Test]
    public void Resolve_InvalidBase64Secret_ThrowsUsageExceptionWithoutTheSecret()
    {
        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = "not base64!";

        UsageException exception = Assert.Throws<UsageException>(() => SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = "https://example.com" }, _environment))!;

        Assert.That(exception.Message, Does.Contain(SchemaTargetResolver.ApiSecretVariable).And.Not.Contain("not base64!"));
    }

    [Test]
    public void Resolve_NoCredentials_ThrowsUsageExceptionWithHint()
    {
        UsageException exception = Assert.Throws<UsageException>(() => SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = "https://example.com" }, _environment))!;

        Assert.That(exception.Hint, Does.Contain(SchemaTargetResolver.ApiSecretVariable).And.Contain("--config").And.Not.Contain("--api-secret"));
    }

    [Test]
    public void Resolve_MissingFolder_ThrowsUsageException()
    {
        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = CloudSecret;

        Assert.Throws<UsageException>(() => SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = "https://example.com", Config = "missing" }, _environment));
    }

    [Test]
    public void Credentials_ToString_DoesNotContainTheKey()
    {
        _environment.Variables[SchemaTargetResolver.ApiKeyVariable] = "my-key";

        SchemaTarget target = SchemaTargetResolver.Resolve(new SchemaTargetSettings { Url = "https://example.com" }, _environment);

        Assert.That(target.ToString(), Does.Not.Contain("my-key"));
    }

    private static string Key(SchemaTarget target) => Encoding.UTF8.GetString(target.Credentials.Key);

    private void WriteCloudJson()
        => File.WriteAllText(Path.Combine(_directory, "umbraco-cloud.json"), $$"""
            {
              "Deploy": {
                "Settings": { "ApiSecret": "{{CloudSecret}}" },
                "Project": {
                  "Workspaces": [
                    { "Id": "{{Guid.NewGuid()}}", "Name": "Development", "Type": "development", "Url": "https://dev.example.com" },
                    { "Id": "{{Guid.NewGuid()}}", "Name": "Live", "Type": "live", "Url": "https://live.example.com/" }
                  ]
                }
              }
            }
            """);

    private void WriteAppSettings()
        => File.WriteAllText(Path.Combine(_directory, "appsettings.json"), $$"""
            {
              "Umbraco": {
                "Deploy": {
                  "Settings": { "ApiSecret": "{{AppSettingsSecret}}" },
                  "Project": {
                    "Workspaces": [
                      { "Id": "{{Guid.NewGuid()}}", "Name": "Staging", "Type": "staging", "Url": "https://staging.example.com" },
                      { "Id": "{{Guid.NewGuid()}}", "Name": "Live", "Type": "live", "Url": "https://other-live.example.com" }
                    ]
                  }
                }
              }
            }
            """);
}
