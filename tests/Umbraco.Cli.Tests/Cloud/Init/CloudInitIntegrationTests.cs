using System.Text.Json.Nodes;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud.Init;

/// <summary>
/// Creates a real Umbraco project: installs Umbraco.Templates into a temporary template store, queries nuget.org, restores and builds (needs network access).
/// </summary>
[TestFixture]
[Category("Integration")]
public class CloudInitIntegrationTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Build servers may still hold files open; the temporary directory is cleaned up by the OS
        }
    }

    [Test]
    public async Task New_CreatesAnUmbracoProjectThatBuilds()
    {
        var environment = new FakeEnvironment(_directory) { CacheDirectory = Path.Combine(_directory, ".cache") };

        CliResult result = await CliRunner.RunAsync(environment, null, "cloud", "init", "--name", "MySite", "--add", "forms", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["built"]!.GetValue<bool>(), Is.True);
        Assert.That(json["packages"]!.AsArray().Select(package => package!["action"]!.GetValue<string>()), Is.All.EqualTo("added"));
        Assert.That(File.Exists(Path.Combine(_directory, "src", "MySite", "MySite.csproj")), Is.True);
        Assert.That(File.Exists(Path.Combine(_directory, "MySite.slnx")), Is.True);
    }
}
