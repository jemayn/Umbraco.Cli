using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud;

/// <summary>
/// Runs against the real Umbraco Cloud API. Opt-in: skipped unless UMBRACO_CLOUD_PROJECT_ID and UMBRACO_CLOUD_API_KEY are set (and never run in CI).
/// </summary>
/// <remarks>
/// The deployment test additionally needs UMBRACO_CLOUD_TEST_ARTIFACT (a zip that is valid for the project) and UMBRACO_CLOUD_TEST_ENVIRONMENT (the alias to deploy to).
/// UMBRACO_CLOUD_API_URL can point the tests at a non-production API.
/// </remarks>
[TestFixture]
[Category("Smoke")]
public class CloudSmokeTests
{
    private readonly IEnvironment _environment = new SystemEnvironment();

    [SetUp]
    public void SetUp()
    {
        if (string.IsNullOrEmpty(_environment.GetVariable("UMBRACO_CLOUD_PROJECT_ID")) || string.IsNullOrEmpty(_environment.GetVariable("UMBRACO_CLOUD_API_KEY")))
        {
            Assert.Ignore("Set UMBRACO_CLOUD_PROJECT_ID and UMBRACO_CLOUD_API_KEY to run the Umbraco Cloud smoke tests.");
        }
    }

    [Test]
    public async Task Credentials_AreAccepted()
    {
        // An unknown deployment is not found (404, exit code 1) rather than unauthorized (401, exit code 5)
        CliResult result = await CliRunner.RunAsync(_environment, null, "cloud", "deployments", "show", Guid.Empty.ToString(), "--no-progress");

        Assert.That(result.ExitCode, Is.Not.EqualTo(ExitCodes.AuthenticationFailed), result.ToString());
    }

    [Test]
    public async Task Deploy_UploadsAndDeploysTheTestArtifact()
    {
        string? artifact = _environment.GetVariable("UMBRACO_CLOUD_TEST_ARTIFACT");
        string? environment = _environment.GetVariable("UMBRACO_CLOUD_TEST_ENVIRONMENT");
        if (string.IsNullOrEmpty(artifact) || string.IsNullOrEmpty(environment))
        {
            Assert.Ignore("Set UMBRACO_CLOUD_TEST_ARTIFACT and UMBRACO_CLOUD_TEST_ENVIRONMENT to run the deployment smoke test.");
        }

        CliResult result = await CliRunner.RunAsync(
            _environment,
            null,
            "cloud",
            "deployments",
            "deploy",
            "--file",
            artifact,
            "--environment",
            environment,
            "--message",
            "Umbraco CLI smoke test",
            "--no-progress",
            "-o",
            "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
    }
}
