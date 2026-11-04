using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class SecretsCommandsTests
{
    private const string Values = """{ "values": { "UMBRACO_DEPLOY_API_SECRET": "s3cr3t-value", "Umbraco:CMS:Identity:ClientSecret": "other-s3cr3t" } }""";

    private FakeEnvironment _environment = null!;
    private FakeHttpMessageHandler _handler = null!;
    private Mock<IGit> _git = null!;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _environment = new FakeEnvironment(_directory);
        _environment.Variables[CloudCredentialsResolver.ProjectIdVariable] = "project-1";
        _environment.Variables[CloudCredentialsResolver.ApiKeyVariable] = "key-1";
        _handler = new FakeHttpMessageHandler();
        _git = new Mock<IGit>();
        _git.Setup(x => x.GetRepositoryRootAsync(_directory, It.IsAny<CancellationToken>())).ReturnsAsync(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task Pull_WritesTheValuesAndIgnoresTheFileWithoutPrintingThem()
    {
        _handler.Respond(HttpStatusCode.OK, Values);
        File.WriteAllText(Path.Combine(_directory, ".gitignore"), "bin/");

        CliResult result = await RunAsync("cloud", "secrets", "pull");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        IReadOnlyDictionary<string, string> written = LocalEnvironmentFile.Read(Path.Combine(_directory, LocalEnvironmentFile.FileName));
        Assert.That(written["UMBRACO_DEPLOY_API_SECRET"], Is.EqualTo("s3cr3t-value"));
        Assert.That(written["Umbraco__CMS__Identity__ClientSecret"], Is.EqualTo("other-s3cr3t"));
        Assert.That(File.ReadAllText(Path.Combine(_directory, ".gitignore")), Is.EqualTo("bin/\n\n# Local development values pulled from Umbraco Cloud (umbraco cloud secrets pull)\n/.env.umbraco\n"));
        Assert.That(result.Stdout, Does.Contain("UMBRACO_DEPLOY_API_SECRET").And.Contain("Added /.env.umbraco to .gitignore"));
        Assert.That(result.ToString(), Does.Not.Contain("s3cr3t"));
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-1/local-development/values"));
    }

    [Test]
    public async Task Pull_AlreadyIgnored_LeavesTheGitignoreAlone()
    {
        _handler.Respond(HttpStatusCode.OK, Values);
        _git.Setup(x => x.IsIgnoredAsync(_directory, LocalEnvironmentFile.FileName, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        CliResult result = await RunAsync("cloud", "secrets", "pull", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["gitignoreUpdated"]!.GetValue<bool>(), Is.False);
        Assert.That(json["names"]!.AsArray().Select(name => name!.GetValue<string>()), Is.EqualTo(new[] { "UMBRACO_DEPLOY_API_SECRET", "Umbraco__CMS__Identity__ClientSecret" }));
        Assert.That(File.Exists(Path.Combine(_directory, ".gitignore")), Is.False);
        Assert.That(result.Stdout, Does.Not.Contain("s3cr3t"));
    }

    [Test]
    public async Task Pull_OutsideAGitRepository_WarnsToNeverCommitIt()
    {
        _handler.Respond(HttpStatusCode.OK, Values);
        _git.Setup(x => x.GetRepositoryRootAsync(_directory, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        CliResult result = await RunAsync("cloud", "secrets", "pull");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(File.Exists(Path.Combine(_directory, LocalEnvironmentFile.FileName)), Is.True);
        Assert.That(result.Stderr, Does.Contain("isn't in a git repository").And.Contain("never commit"));
    }

    [Test]
    public async Task Pull_MissingApiKey_ExplainsWhereToGetIt()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ApiKeyVariable);

        CliResult result = await RunAsync("cloud", "secrets", "pull");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain(CloudCredentialsResolver.ApiKeyVariable).And.Contain("CI/CD Flow"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Pull_EndpointNotAvailable_FailsWithoutWritingAFile()
    {
        _handler.Respond(HttpStatusCode.NotFound);

        CliResult result = await RunAsync("cloud", "secrets", "pull");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Error), result.ToString());
        Assert.That(result.Stderr, Does.Contain("local development values"));
        Assert.That(File.Exists(Path.Combine(_directory, LocalEnvironmentFile.FileName)), Is.False);
    }

    [Test]
    public async Task PulledValues_AreUsedAsEnvironmentVariables()
    {
        Directory.CreateDirectory(Path.Combine(_directory, ".git"));
        _environment.Variables.Remove(CloudCredentialsResolver.ApiKeyVariable);
        File.WriteAllText(Path.Combine(_directory, LocalEnvironmentFile.FileName), $"{CloudCredentialsResolver.ApiKeyVariable}=key-from-file\n");
        _handler.Respond(HttpStatusCode.OK, Values);

        CliResult result = await RunAsync("cloud", "secrets", "pull");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(_handler.Requests.Single().Headers[CloudApiClient.ApiKeyHeaderName], Is.EqualTo("key-from-file"));
    }

    private Task<CliResult> RunAsync(params string[] args)
        => CliRunner.RunAsync(
            _environment,
            services =>
            {
                services.AddSingleton(_git.Object);
                services.AddHttpClient<ICloudApiClient, CloudApiClient>().ConfigurePrimaryHttpMessageHandler(() => _handler);
            },
            args);
}
