using System.Text.Json.Nodes;
using NUnit.Framework;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud.Init;

[TestFixture]
public class CloudInitFilesTests
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
    public void CloudScaffold_WritesRootAndProjectFiles()
    {
        string project = Directory.CreateDirectory(Path.Combine(_directory, "src", "MySite")).FullName;

        IReadOnlyList<string> written = CloudScaffold.Write(_directory, project, "MySite", 17, gitDeployment: true);

        Assert.That(written.Select(path => Path.GetRelativePath(_directory, path).Replace('\\', '/')), Is.EquivalentTo(new[]
        {
            "README.md", "src/MySite/appsettings.Staging.json", "src/MySite/appsettings.Production.json",
        }));
        Assert.That(File.ReadAllText(Path.Combine(_directory, "README.md")), Does.Contain("cd src/MySite").And.Contain("└── README.md").And.Not.Contain("UmbracoProject").And.Not.Contain("NuGet.config"));
        Assert.That(JsonNode.Parse(File.ReadAllText(Path.Combine(project, "appsettings.Production.json")))!["Umbraco"]!["CMS"]!["Hosting"]!["Debug"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void CloudScaffold_ForArtifactDeployments_WritesNoReadme()
    {
        string project = Directory.CreateDirectory(Path.Combine(_directory, "src", "MySite")).FullName;

        IReadOnlyList<string> written = CloudScaffold.Write(_directory, project, "MySite", 17, gitDeployment: false);

        Assert.That(written.Select(Path.GetFileName), Does.Not.Contain("README.md"));
        Assert.That(CloudScaffold.GetRootFileNames(gitDeployment: false), Is.Empty);
    }

    [Test]
    public void DotUmbracoFile_Create_UsesForwardSlashes()
        => Assert.That(DotUmbracoFile.Create("src\\MySite", "MySite.csproj"), Is.EqualTo("[project]\nbase = \"src/MySite\"\ncsproj = \"MySite.csproj\"\n"));

    [Test]
    public void DotUmbracoFile_Ensure_WritesAtTheGitRoot()
    {
        Directory.CreateDirectory(Path.Combine(_directory, ".git"));
        string project = CreateProject("src/Web/Web.csproj");

        string? written = DotUmbracoFile.Ensure(project, _ => Assert.Fail("No warning expected"));

        Assert.That(written, Is.EqualTo(Path.Combine(_directory, ".umbraco")));
        Assert.That(DotUmbracoFile.Resolve(written!), Is.EqualTo(project));
    }

    [Test]
    public void DotUmbracoFile_FindRoot_WithoutGitUsesTheSolutionDirectoryElseTheProject()
    {
        string project = CreateProject("src/Web/Web.csproj");
        Assert.That(DotUmbracoFile.FindRoot(Path.GetDirectoryName(project)!), Is.EqualTo(Path.GetDirectoryName(project)));

        File.WriteAllText(Path.Combine(_directory, "Site.slnx"), "<Solution />");
        Assert.That(DotUmbracoFile.FindRoot(Path.GetDirectoryName(project)!), Is.EqualTo(_directory));
    }

    [Test]
    public void DotUmbracoFile_Ensure_KeepsAnExistingFileAndWarnsWhenItPointsElsewhere()
    {
        string project = CreateProject("src/Web/Web.csproj");
        File.WriteAllText(Path.Combine(_directory, ".umbraco"), DotUmbracoFile.Create("src/Other", "Other.csproj"));
        var warnings = new List<string>();

        string? written = DotUmbracoFile.Ensure(project, warnings.Add);

        Assert.That(written, Is.Null);
        Assert.That(warnings.Single(), Does.Contain("Other.csproj"));
    }

    [Test]
    public void EnsureUmbracoCloudJson_CreatesPlaceholders()
    {
        var files = new List<string>();

        string state = CloudProjectSettings.EnsureUmbracoCloudJson(_directory, given: null, files);

        Assert.That(state, Is.EqualTo("placeholder"));
        JsonNode json = JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, "umbraco-cloud.json")))!;
        Assert.That(json["Identity"]!["ClientId"]!.GetValue<string>(), Is.EqualTo(IdentityValues.PlaceholderClientId));
        Assert.That(json["Identity"]!["ClientSecret"]!.GetValue<string>(), Is.EqualTo(IdentityValues.PlaceholderClientSecret));
        Assert.That(files, Has.Count.EqualTo(1));
    }

    [Test]
    public void EnsureUmbracoCloudJson_ExistingFile_IsUnchangedUnlessValuesAreGiven()
    {
        string path = Path.Combine(_directory, "umbraco-cloud.json");
        const string original = "{\n  // From Umbraco Cloud\n  \"Deploy\": { \"Settings\": { \"ApiKey\": \"k\" } }\n}";
        File.WriteAllText(path, original);

        Assert.That(CloudProjectSettings.EnsureUmbracoCloudJson(_directory, given: null, []), Is.EqualTo("unchanged"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(original));

        Assert.That(CloudProjectSettings.EnsureUmbracoCloudJson(_directory, new IdentityValues("id", "secret", IsPlaceholder: false), []), Is.EqualTo("provided"));
        string updated = File.ReadAllText(path);
        Assert.That(updated, Does.Contain("// From Umbraco Cloud").And.Contain("\"ApiKey\": \"k\"").And.Contain("\"ClientSecret\": \"secret\""));
    }

    [Test]
    public void EnsureLicenses_SetsMissingCloudLicensesOnly()
    {
        string path = Path.Combine(_directory, "appsettings.json");
        File.WriteAllText(path, """{ "Umbraco": { "Licenses": { "Products": { "Umbraco.Forms": "MY-KEY" } } } }""");
        var files = new List<string>();

        CloudProjectSettings.EnsureLicenses(_directory, ["Umbraco.Deploy", "Umbraco.Forms"], files);
        CloudProjectSettings.EnsureLicenses(_directory, ["Umbraco.Deploy"], files);

        JsonNode products = JsonNode.Parse(File.ReadAllText(path))!["Umbraco"]!["Licenses"]!["Products"]!;
        Assert.That(products["Umbraco.Deploy"]!.GetValue<string>(), Is.EqualTo("UMBRACO-CLOUD"));
        Assert.That(products["Umbraco.Forms"]!.GetValue<string>(), Is.EqualTo("MY-KEY"));
        Assert.That(files, Has.Count.EqualTo(1));
    }

    [Test]
    public void IdentityValues_Resolve_PrefersOptionsOverEnvironmentVariables()
    {
        var environment = new FakeEnvironment();
        environment.Variables[IdentityValues.ClientIdVariable] = "env-id";
        environment.Variables[IdentityValues.ClientSecretVariable] = "env-secret";

        Assert.That(IdentityValues.Resolve(null, null, new FakeEnvironment()), Is.Null);
        Assert.That(IdentityValues.Resolve(null, null, environment), Is.EqualTo(new IdentityValues("env-id", "env-secret", false)));
        Assert.That(IdentityValues.Resolve("id", "secret", environment), Is.EqualTo(new IdentityValues("id", "secret", false)));
        Assert.Throws<UsageException>(() => IdentityValues.Resolve("id", null, new FakeEnvironment()));
        Assert.That(new IdentityValues("id", "secret", false).ToString(), Does.Not.Contain("secret"));
    }

    [Test]
    public void SolutionFile_AddSolutionItems_AddsASortedFolderFirst()
    {
        string slnx = Path.Combine(_directory, "Site.slnx");
        File.WriteAllText(slnx, "<Solution>\n  <Folder Name=\"/src/\">\n    <Project Path=\"src/Site/Site.csproj\" />\n  </Folder>\n</Solution>\n");

        SolutionFile.AddSolutionItems(slnx, ["README.md", ".umbraco", "README.md"]);

        Assert.That(File.ReadAllText(slnx), Is.EqualTo(
            "<Solution>\n" +
            "  <Folder Name=\"/Solution Items/\">\n" +
            "    <File Path=\".umbraco\" />\n" +
            "    <File Path=\"README.md\" />\n" +
            "  </Folder>\n" +
            "  <Folder Name=\"/src/\">\n" +
            "    <Project Path=\"src/Site/Site.csproj\" />\n" +
            "  </Folder>\n" +
            "</Solution>\n"));
    }

    [Test]
    public void GitignoreHints_ListsMissingEntries()
    {
        Assert.That(GitignoreHints.GetMissing(_directory), Is.EqualTo(GitignoreHints.ExpectedEntries));

        File.WriteAllLines(Path.Combine(_directory, ".gitignore"), ["bin/", "**/wwwroot/media", " **/umbraco/Logs/ "]);
        Assert.That(GitignoreHints.GetMissing(_directory), Is.EqualTo(new[] { "**/umbraco/Data/TEMP/", "**/umbraco/Deploy/deploy*" }));
    }

    private string CreateProject(string relativePath)
    {
        string path = Path.Combine(_directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
        return path;
    }
}
