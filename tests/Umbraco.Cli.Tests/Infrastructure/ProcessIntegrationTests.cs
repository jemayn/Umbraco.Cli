using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Tests.Infrastructure;

/// <summary>
/// Runs the real git and dotnet executables.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ProcessIntegrationTests
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
        // git makes its object files read-only
        foreach (string file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task Git_ListsTrackedAndUntrackedButNotIgnoredFiles()
    {
        var runner = new ProcessRunner();
        await RunGitAsync(runner, "init", "-q");
        File.WriteAllText(Path.Combine(_directory, ".gitignore"), "bin/\n");
        File.WriteAllText(Path.Combine(_directory, "Site.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(_directory, "bin"));
        File.WriteAllText(Path.Combine(_directory, "bin", "Site.dll"), "binary");

        IReadOnlyList<string> files = await new Git(runner).ListFilesAsync(_directory, CancellationToken.None);

        Assert.That(files, Is.EquivalentTo(new[] { ".gitignore", "Site.csproj" }));
    }

    [Test]
    public async Task Git_GetsHeadSubject()
    {
        var runner = new ProcessRunner();
        await RunGitAsync(runner, "init", "-q");
        File.WriteAllText(Path.Combine(_directory, "a.txt"), "a");
        await RunGitAsync(runner, "add", ".");
        await RunGitAsync(runner, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "Add a");

        Assert.That(await new Git(runner).GetHeadSubjectAsync(_directory, CancellationToken.None), Is.EqualTo("Add a"));
    }

    [Test]
    public async Task Dotnet_ReportsSdkVersion10OrLater()
    {
        string version = await new DotnetCli(new ProcessRunner()).GetSdkVersionAsync(_directory, CancellationToken.None);

        Assert.DoesNotThrow(() => Umbraco.Cli.Deploy.ProjectDetection.DeployPackageDetector.EnsureSupportedSdk(version));
    }

    [Test]
    public void Dotnet_PackageListOutputIsUnderstood()
    {
        // A project without packages restores offline; detection then reports that Umbraco Deploy is missing
        string project = Path.Combine(_directory, "Site.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var detector = new Umbraco.Cli.Deploy.ProjectDetection.DeployPackageDetector(new DotnetCli(new ProcessRunner()));

        UsageException exception = Assert.ThrowsAsync<UsageException>(() => detector.DetectAsync(project, interactive: false, CancellationToken.None))!;

        Assert.That(exception.Message, Does.Contain("doesn't reference Umbraco.Deploy.OnPrem or Umbraco.Deploy.Cloud"));
    }

    [Test]
    public void ProcessRunner_MissingExecutable_ThrowsCliException()
        => Assert.ThrowsAsync<CliException>(() => new ProcessRunner().RunAsync("umbraco-cli-missing-executable", [], _directory, CancellationToken.None));

    private async Task RunGitAsync(ProcessRunner runner, params string[] arguments)
    {
        ProcessResult result = await runner.RunAsync("git", arguments, _directory, CancellationToken.None);
        Assert.That(result.ExitCode, Is.Zero, result.StandardError);
    }
}
