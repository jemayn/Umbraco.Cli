using System.IO.Compression;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class ArtifactZipBuilderTests
{
    private string _directory = null!;
    private Mock<IGit> _git = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _git = new Mock<IGit>(MockBehavior.Strict);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public async Task CreateAsync_PublishedOutput_ZipsEverythingExceptGit()
    {
        WriteFiles("Umbraco.Core.dll", "wwwroot/css/site.css", "appsettings.json", ".git/config", ".gitignore");

        using ArtifactPackage package = await CreateBuilder().CreateAsync(_directory, type: null, CancellationToken.None);

        Assert.That(package.Type, Is.EqualTo(DeploymentType.Artifact));
        Assert.That(ReadEntries(package), Is.EquivalentTo(new[] { "Umbraco.Core.dll", "wwwroot/css/site.css", "appsettings.json", ".gitignore" }));
    }

    [Test]
    public async Task CreateAsync_SourceCode_ZipsTheFilesGitListsAndTheGitignore()
    {
        WriteFiles("Site.csproj", "Program.cs", ".umbraco", ".gitignore", "bin/Site.dll", "deleted.txt");
        File.Delete(Path.Combine(_directory, "deleted.txt"));
        _git.Setup(x => x.ListFilesAsync(Path.GetFullPath(_directory), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Site.csproj", "Program.cs", ".umbraco", "deleted.txt"]);

        using ArtifactPackage package = await CreateBuilder().CreateAsync(_directory, type: null, CancellationToken.None);

        Assert.That(package.Type, Is.EqualTo(DeploymentType.Git));
        Assert.That(package.HasDotUmbraco, Is.True);
        Assert.That(ReadEntries(package), Is.EquivalentTo(new[] { "Site.csproj", "Program.cs", ".umbraco", ".gitignore" }));
    }

    [Test]
    public async Task CreateAsync_SourceCodeWithoutDotUmbraco_ReportsIt()
    {
        WriteFiles("Site.csproj");
        _git.Setup(x => x.ListFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(["Site.csproj"]);

        using ArtifactPackage package = await CreateBuilder().CreateAsync(_directory, type: null, CancellationToken.None);

        Assert.That(package.HasDotUmbraco, Is.False);
    }

    [Test]
    public async Task CreateAsync_TypeOverride_SkipsDetection()
    {
        WriteFiles("index.html");

        using ArtifactPackage package = await CreateBuilder().CreateAsync(_directory, DeploymentType.Artifact, CancellationToken.None);

        Assert.That(ReadEntries(package), Is.EqualTo(new[] { "index.html" }));
    }

    [Test]
    public void CreateAsync_Undetectable_ThrowsUsageExceptionSuggestingType()
    {
        WriteFiles("index.html");

        UsageException exception = Assert.ThrowsAsync<UsageException>(() => CreateBuilder().CreateAsync(_directory, type: null, CancellationToken.None))!;

        Assert.That(exception.Hint, Does.Contain("--type"));
    }

    [Test]
    public async Task Dispose_DeletesTheTemporaryZip()
    {
        WriteFiles("Umbraco.Core.dll");
        ArtifactPackage package = await CreateBuilder().CreateAsync(_directory, type: null, CancellationToken.None);

        package.Dispose();

        Assert.That(File.Exists(package.ZipPath), Is.False);
    }

    private static List<string> ReadEntries(ArtifactPackage package)
    {
        using ZipArchive zip = ZipFile.OpenRead(package.ZipPath);
        return zip.Entries.Select(entry => entry.FullName).ToList();
    }

    private ArtifactZipBuilder CreateBuilder() => new(_git.Object);

    private void WriteFiles(params string[] paths)
    {
        foreach (string path in paths)
        {
            string fullPath = Path.Combine(_directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, path);
        }
    }
}
