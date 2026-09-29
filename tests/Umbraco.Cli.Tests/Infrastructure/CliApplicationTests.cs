using System.CommandLine;
using System.Reflection;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class CliApplicationTests
{
    [Test]
    public async Task Help_ListsCommandGroups()
    {
        CliResult result = await CliRunner.RunAsync("--help");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain("umbraco [command] [options]").And.Contain("cloud").And.Contain("deploy"));
    }

    [Test]
    public void PackageTags_IncludeEachCommandGroup()
    {
        string[] tags = typeof(CliApplication).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PackageTags")
            .Value!
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Command rootCommand = new CliApplication(new FakeEnvironment(), TextWriter.Null, TextWriter.Null).CreateRootCommand();

        // A new product or service with commands needs its own tag (PackageTags in Directory.Build.props)
        Assert.That(tags, Is.SupersetOf(rootCommand.Subcommands.Select(command => command.Name)));
    }

    [Test]
    public async Task SubcommandHelp_UsesTheToolCommandName()
    {
        CliResult result = await CliRunner.RunAsync("deploy", "init", "--help");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain("umbraco deploy init [options]"));
    }

    [Test]
    public async Task UnknownCommand_ReturnsUsageExitCode()
    {
        CliResult result = await CliRunner.RunAsync("nonsense");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("error:"));
    }

    [Test]
    public async Task InvalidOutputFormat_ReturnsUsageExitCode()
    {
        CliResult result = await CliRunner.RunAsync("cloud", "deployments", "show", "42", "-o", "yaml");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
    }

    [Test]
    public async Task MissingCredentials_ReturnsUsageExitCodeWithHint()
    {
        CliResult result = await CliRunner.RunAsync("cloud", "deployments", "show", "42");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("error: No Umbraco Cloud project ID was specified.").And.Contain("UMBRACO_CLOUD_PROJECT_ID"));
        Assert.That(result.Stdout, Is.Empty);
    }
}
