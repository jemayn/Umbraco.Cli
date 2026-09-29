using NUnit.Framework;
using Umbraco.Cli.Deploy.ProjectDetection;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class ProjectLocatorTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Locate_SingleProjectInCurrentDirectory()
    {
        string project = Touch("Site.csproj");

        Assert.That(ProjectLocator.Locate(null, _directory), Is.EqualTo(project));
    }

    [Test]
    public void Locate_RelativeProjectFile()
    {
        string project = Touch("src/Site/Site.csproj");

        Assert.That(ProjectLocator.Locate("src/Site/Site.csproj", _directory), Is.EqualTo(project));
    }

    [Test]
    public void Locate_Directory()
    {
        string project = Touch("src/Site/Site.csproj");

        Assert.That(ProjectLocator.Locate("src/Site", _directory), Is.EqualTo(project));
    }

    [Test]
    public void Locate_NoProject_ThrowsWithHint()
    {
        UsageException exception = Assert.Throws<UsageException>(() => ProjectLocator.Locate(null, _directory))!;

        Assert.That(exception.Hint, Does.Contain("--project"));
    }

    [Test]
    public void Locate_SeveralProjects_ListsThem()
    {
        Touch("A.csproj");
        Touch("B.csproj");

        UsageException exception = Assert.Throws<UsageException>(() => ProjectLocator.Locate(null, _directory))!;

        Assert.That(exception.Message, Does.Contain("A.csproj, B.csproj"));
    }

    [Test]
    public void Locate_NotAProjectFile_Throws()
    {
        Touch("Site.sln");

        Assert.Throws<UsageException>(() => ProjectLocator.Locate("Site.sln", _directory));
    }

    private string Touch(string relativePath)
    {
        string path = Path.Combine(_directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
        return path;
    }
}
