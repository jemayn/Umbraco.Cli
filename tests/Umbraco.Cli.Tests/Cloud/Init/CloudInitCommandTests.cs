using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.NuGet;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Tests.Deploy;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud.Init;

[TestFixture]
public class CloudInitCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private string _directory = null!;
    private FakeEnvironment _environment = null!;
    private Mock<IDotnetCli> _dotnet = null!;
    private Mock<IPrompter> _prompter = null!;
    private List<string[]> _commands = null!;
    private FakeNuGetClient _nuGet = null!;
    private Mock<ICloudApiClient> _cloudApi = null!;
    private Mock<IGit> _git = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _environment = new FakeEnvironment(_directory) { CacheDirectory = Path.Combine(_directory, ".cache") };
        _commands = [];
        _prompter = new Mock<IPrompter>(MockBehavior.Strict);
        _cloudApi = new Mock<ICloudApiClient>(MockBehavior.Strict);
        _git = new Mock<IGit>();
        _dotnet = new Mock<IDotnetCli>();
        _dotnet.Setup(x => x.GetSdkVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("10.0.401");
        _dotnet
            .Setup(x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, CancellationToken>((arguments, _, _) => Simulate(arguments))
            .ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

        _nuGet = new FakeNuGetClient()
            .Add("Umbraco.Cms", "17.0.0", new DateTimeOffset(2025, 11, 27, 0, 0, 0, TimeSpan.Zero))
            .Add("Umbraco.Cms", "18.0.0", new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero))
            .Add("Umbraco.Templates", "17.7.0")
            .Add("Umbraco.Templates", "18.2.0")
            .Add("Umbraco.Templates", "18.3.0-rc")
            .Add("Umbraco.Cloud.Cms", "17.2.3", dependencies: "Umbraco.Cms.Core [17.0.0, 18.0.0)")
            .Add("Umbraco.Cloud.Cms", "18.0.3", dependencies: "Umbraco.Cms.Core [18.0.0, 19.0.0)")
            .Add("Umbraco.Cloud.StorageProviders.AzureBlob", "17.1.0")
            .Add("Umbraco.Cloud.StorageProviders.AzureBlob", "18.0.0")
            .Add("Umbraco.Deploy.Cloud", "17.3.1")
            .Add("Umbraco.Deploy.Cloud", "18.1.1")
            .Add("Umbraco.Forms", "17.5.2")
            .Add("Umbraco.Forms", "18.1.3")
            .Add("Umbraco.Forms.Deploy", "17.0.1")
            .Add("Umbraco.Forms.Deploy", "18.0.1");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public async Task New_CreatesTheCloudScaffoldAndAddsPackages()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--deployment-type", "git", "--add", "forms", "-o", "json", "--", "--development-database-type", "SQLite");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["created"]!.GetValue<bool>(), Is.True);
        Assert.That(json["deploymentType"]!.GetValue<string>(), Is.EqualTo("git"));
        Assert.That(json["cmsVersion"]!.GetValue<string>(), Is.EqualTo("18.2.0"));
        Assert.That(json["releaseType"]!.GetValue<string>(), Is.EqualTo("STS"));
        Assert.That(json["lifecyclePhase"]!.GetValue<string>(), Is.EqualTo("active"));
        Assert.That(json["identity"]!.GetValue<string>(), Is.EqualTo("placeholder"));
        Assert.That(json["built"]!.GetValue<bool>(), Is.True);
        Assert.That(json["packages"]!.AsArray().Select(package => $"{package!["id"]} {package["version"]} {package["action"]}"), Is.EqualTo(new[]
        {
            "Umbraco.Cloud.Cms 18.0.3 added",
            "Umbraco.Cloud.StorageProviders.AzureBlob 18.0.0 added",
            "Umbraco.Deploy.Cloud 18.1.1 added",
            "Umbraco.Forms 18.1.3 added",
            "Umbraco.Forms.Deploy 18.0.1 added",
        }));

        string project = Path.Combine(_directory, "src", "MySite");
        Assert.That(File.ReadAllText(Path.Combine(_directory, ".umbraco")), Does.Contain("base = \"src/MySite\""));
        Assert.That(File.Exists(Path.Combine(_directory, "README.md")), Is.True);
        Assert.That(File.Exists(Path.Combine(_directory, "Directory.Packages.props")), Is.True);
        Assert.That(File.Exists(Path.Combine(project, "Directory.Packages.props")), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(_directory, "MySite.slnx")), Does.Contain("<File Path=\".umbraco\" />"));
        Assert.That(File.ReadAllText(Path.Combine(project, "appsettings.json")), Does.Contain("\"Umbraco.Forms\": \"UMBRACO-CLOUD\""));

        string[] newProject = _commands.Single(command => command[0] == "new" && command[1] == "umbraco");
        Assert.That(newProject, Does.Contain("--package-management").And.Contain("--use-https-redirect").And.Contain("--debug:custom-hive"));
        Assert.That(newProject.SkipWhile(argument => argument != "--development-mode").Skip(1).First(), Is.EqualTo("IDEDevelopment"));
        Assert.That(newProject, Does.Contain("--minimal-gitignore").And.Not.Contain("--exclude-gitignore"));
        Assert.That(_commands.Any(command => command is ["new", "gitignore", ..]), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(project, ".gitignore")), Does.Contain("/umbraco/Deploy/deploy*"));
        Assert.That(File.ReadAllText(Path.Combine(_directory, "MySite.slnx")), Does.Contain("<File Path=\".gitignore\" />"));
        Assert.That(File.Exists(Path.Combine(_directory, ".dockerignore")), Is.False);
        Assert.That(newProject[^2..], Is.EqualTo(new[] { "--development-database-type", "SQLite" }));
        Assert.That(_commands.Single(command => command[0] == "new" && command[1] == "install")[2], Is.EqualTo("Umbraco.Templates@18.2.0"));
        Assert.That(_commands.Select(command => command[0]), Does.Contain("restore").And.Contain("build"));
    }

    [TestCase("backoffice", "BackofficeDevelopment")]
    [TestCase("ide", "IDEDevelopment")]
    public async Task New_DevelopmentMode_IsPassedToTheTemplate(string mode, string expected)
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--development-mode", mode, "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string[] newProject = _commands.Single(command => command[0] == "new" && command[1] == "umbraco");
        Assert.That(newProject.Count(argument => argument == "--development-mode"), Is.EqualTo(1));
        Assert.That(newProject.SkipWhile(argument => argument != "--development-mode").Skip(1).First(), Is.EqualTo(expected));
    }

    [Test]
    public async Task New_DevelopmentModeInTemplateOptions_WinsOverTheDefault()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build", "--", "-dm", "BackofficeDevelopment");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string[] newProject = _commands.Single(command => command[0] == "new" && command[1] == "umbraco");
        Assert.That(newProject, Does.Not.Contain("--development-mode"));
        Assert.That(newProject[^2..], Is.EqualTo(new[] { "-dm", "BackofficeDevelopment" }));
    }

    [Test]
    public async Task New_TextOutput_RecommendsDeployInitAndARunBeforePublishing()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain("umbraco deploy init --project").And.Contain("Run the project locally first").And.Contain("dotnet publish"));
    }

    [Test]
    public async Task Existing_DevelopmentMode_ReturnsUsageExitCode()
    {
        CreateExistingProject(("Umbraco.Cms", "17.7.0"));

        CliResult result = await RunAsync("cloud", "init", "--development-mode", "ide");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
    }

    [Test]
    public async Task New_ArtifactDeployment_IsTheDefaultAndHasNoDotUmbracoOrReadme()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(result.Stdout)!["deploymentType"]!.GetValue<string>(), Is.EqualTo("artifact"));
        Assert.That(File.Exists(Path.Combine(_directory, ".umbraco")), Is.False);
        Assert.That(File.Exists(Path.Combine(_directory, "README.md")), Is.False);
        Assert.That(File.Exists(Path.Combine(_directory, "NuGet.config")), Is.False);
        string solution = File.ReadAllText(Path.Combine(_directory, "MySite.slnx"));
        Assert.That(solution, Does.Not.Contain(".umbraco").And.Not.Contain("README.md"));
    }

    [Test]
    public async Task New_TextOutput_ShowsTheNextStepsForTheDeploymentType()
    {
        CliResult artifact = await RunAsync("cloud", "init", "--name", "MySite", "--output", "a", "--no-build");
        CliResult git = await RunAsync("cloud", "init", "--name", "MySite", "--output", "g", "--deployment-type", "git", "--no-build");

        Assert.That(artifact.Stdout, Does.Contain("dotnet publish").And.Not.Contain("Git deployment"));
        Assert.That(git.Stdout, Does.Contain("Git deployment").And.Not.Contain("dotnet publish"));
    }

    [Test]
    public async Task New_ExcludeGitignore_CreatesNoGitignoreFiles()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--exclude-gitignore", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string[] newProject = _commands.Single(command => command[0] == "new" && command[1] == "umbraco");
        Assert.That(newProject, Does.Contain("--exclude-gitignore").And.Not.Contain("--minimal-gitignore"));
        Assert.That(_commands.Any(command => command is ["new", "gitignore", ..]), Is.False);
        Assert.That(File.Exists(Path.Combine(_directory, ".gitignore")), Is.False);
        Assert.That(File.Exists(Path.Combine(_directory, "src", "MySite", ".gitignore")), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(_directory, "MySite.slnx")), Does.Not.Contain(".gitignore"));
    }

    [Test]
    public async Task New_ExistingGitignore_IsAConflictUnlessExcluded()
    {
        File.WriteAllText(Path.Combine(_directory, ".gitignore"), "mine");

        CliResult conflict = await RunAsync("cloud", "init", "--name", "MySite", "--no-build");
        CliResult excluded = await RunAsync("cloud", "init", "--name", "MySite", "--exclude-gitignore", "--no-build");

        Assert.That(conflict.ExitCode, Is.EqualTo(ExitCodes.Usage), conflict.ToString());
        Assert.That(conflict.Stderr, Does.Contain(".gitignore"));
        Assert.That(excluded.ExitCode, Is.EqualTo(ExitCodes.Success), excluded.ToString());
        Assert.That(File.ReadAllText(Path.Combine(_directory, ".gitignore")), Is.EqualTo("mine"));
    }

    [Test]
    public async Task New_ReusesTheCachedTemplate()
    {
        await RunAsync("cloud", "init", "--name", "First", "--output", "a", "--no-build");
        await RunAsync("cloud", "init", "--name", "Second", "--output", "b", "--no-build");

        Assert.That(_commands.Count(command => command[0] == "new" && command[1] == "install"), Is.EqualTo(1));
    }

    [Test]
    public async Task New_Lts_UsesTheNewestLtsVersion()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--version", "lts", "--no-build", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(result.Stdout)!["cmsVersion"]!.GetValue<string>(), Is.EqualTo("17.7.0"));
    }

    [Test]
    public async Task New_EndOfLife_IsRefusedUnlessAllowed()
    {
        _nuGet.Add("Umbraco.Cms", "17.0.1").Add("Umbraco.Templates", "17.8.0");
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero));

        CliResult refused = await RunAsync(timeProvider, "cloud", "init", "--name", "Old", "--version", "17", "--output", "a");
        CliResult allowed = await RunAsync(timeProvider, "cloud", "init", "--name", "Old", "--version", "17", "--output", "b", "--allow-end-of-life", "--no-build");

        Assert.That(refused.ExitCode, Is.EqualTo(ExitCodes.Usage), refused.ToString());
        Assert.That(refused.Stderr, Does.Contain("end-of-life"));
        Assert.That(allowed.ExitCode, Is.EqualTo(ExitCodes.Success), allowed.ToString());
        Assert.That(allowed.Stderr, Does.Contain("warning: Umbraco 17 is end-of-life"));
    }

    [Test]
    public async Task New_OutputWithFilesItWouldCreate_ReturnsUsageExitCode()
    {
        File.WriteAllText(Path.Combine(_directory, "Directory.Packages.props"), "x");
        Directory.CreateDirectory(Path.Combine(_directory, "src", "MySite"));

        CliResult result = await RunAsync("cloud", "init", "--name", "MySite");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("already contains").And.Contain("Directory.Packages.props").And.Contain("MySite"));
        Assert.That(_commands, Is.Empty);
    }

    [Test]
    public async Task New_OutputWithOtherFiles_IsAllowed()
    {
        File.WriteAllText(Path.Combine(_directory, "setup.ps1"), "x");
        Directory.CreateDirectory(Path.Combine(_directory, "nupkg"));

        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(File.ReadAllText(Path.Combine(_directory, "setup.ps1")), Is.EqualTo("x"));
    }

    [Test]
    public async Task New_Interactive_PromptsForTheVersionAndProducts()
    {
        _environment.IsInputRedirected = false;
        _environment.IsErrorRedirected = false;
        _prompter
            .Setup(x => x.SelectAsync("Which Umbraco version?", It.Is<IReadOnlyList<string>>(choices => choices.Count == 2 && choices[0].StartsWith("Umbraco 18.2.0 (STS")), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<string> choices, CancellationToken _) => choices[1]);
        _prompter
            .Setup(x => x.SelectManyAsync(It.IsAny<string>(), It.Is<IReadOnlyList<string>>(choices => choices.SequenceEqual(new[] { "Umbraco Forms (forms)" })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Umbraco Forms (forms)"]);

        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build", "--no-progress", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["cmsVersion"]!.GetValue<string>(), Is.EqualTo("17.7.0"));
        Assert.That(json["packages"]!.AsArray().Select(package => package!["id"]!.GetValue<string>()), Does.Contain("Umbraco.Forms"));
    }

    [Test]
    public async Task Existing_AddsMissingPackagesAndKeepsReferencedOnes()
    {
        string project = CreateExistingProject(("Umbraco.Cms", "17.7.0"), ("Umbraco.Deploy.Cloud", "16.0.6"));

        CliResult result = await RunAsync("cloud", "init", "--no-build", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["created"]!.GetValue<bool>(), Is.False);
        Assert.That(json["cmsVersion"]!.GetValue<string>(), Is.EqualTo("17.7.0"));
        Assert.That(json["releaseType"]!.GetValue<string>(), Is.EqualTo("LTS"));
        Assert.That(json["packages"]!.AsArray().Select(package => $"{package!["id"]} {package["version"]} {package["action"]}"), Is.EqualTo(new[]
        {
            "Umbraco.Cloud.Cms 17.2.3 added",
            "Umbraco.Cloud.StorageProviders.AzureBlob 17.1.0 added",
            "Umbraco.Deploy.Cloud 16.0.6 referenced",
        }));
        Assert.That(result.Stderr, Does.Contain("warning: Umbraco.Deploy.Cloud 16.0.6 doesn't match Umbraco 17"));
        Assert.That(result.Stderr, Does.Contain("hint: The Cloud scaffold's .gitignore also ignores"));

        string[] add = _commands.First(command => command[0] == "package");
        Assert.That(add, Is.EqualTo(new[] { "package", "add", "Umbraco.Cloud.Cms", "--version", "17.2.3", "--project", project, "--no-restore" }));
        Assert.That(File.Exists(Path.Combine(_directory, ".umbraco")), Is.False);
        Assert.That(File.Exists(Path.Combine(_directory, "umbraco-cloud.json")), Is.True);
    }

    [Test]
    public async Task Existing_WithCentralPackageManagement_AddsPackagesWithARestore()
    {
        string project = CreateExistingProject(("Umbraco.Cms", "17.7.0"));
        File.WriteAllText(Path.Combine(_directory, "Directory.Packages.props"), "<Project />");

        CliResult result = await RunAsync("cloud", "init", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string[] add = _commands.First(command => command[0] == "package");
        Assert.That(add, Is.EqualTo(new[] { "package", "add", "Umbraco.Cloud.Cms", "--version", "17.2.3", "--project", project }));
    }

    [Test]
    public async Task Existing_GitDeployment_WritesDotUmbraco()
    {
        CreateExistingProject(("Umbraco.Cms", "17.7.0"));

        CliResult result = await RunAsync("cloud", "init", "--deployment-type", "git", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(File.ReadAllText(Path.Combine(_directory, ".umbraco")), Does.Contain("csproj = \"Site.csproj\""));
    }

    [Test]
    public async Task Existing_OlderThan17_ReturnsUsageExitCode()
    {
        CreateExistingProject(("Umbraco.Cms", "13.16.2"));

        CliResult result = await RunAsync("cloud", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("supports Umbraco 17 and later"));
    }

    [Test]
    public async Task Existing_NotAnUmbracoProject_ReturnsUsageExitCode()
    {
        CreateExistingProject(("Newtonsoft.Json", "13.0.3"));

        CliResult result = await RunAsync("cloud", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("is not an Umbraco project"));
    }

    [Test]
    public async Task Existing_NewProjectOptions_ReturnUsageExitCode()
    {
        CreateExistingProject(("Umbraco.Cms", "17.7.0"));

        CliResult result = await RunAsync("cloud", "init", "--version", "18");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("only apply when creating"));
    }

    [Test]
    public async Task UnknownProduct_ReturnsUsageExitCode()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--add", "commerce");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Use --add with one of: forms"));
    }

    [Test]
    public async Task NoProjectAndNotInteractive_ReturnsUsageExitCodeSuggestingName()
    {
        CliResult result = await RunAsync("cloud", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("--name"));
    }

    [Test]
    public async Task Help_HasNoIdentityOptions()
    {
        CliResult result = await RunAsync("cloud", "init", "--help");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Not.Contain("--identity-client"));
    }

    [Test]
    public async Task IdentityVariables_AreNotWrittenToUmbracoCloudJson()
    {
        _environment.Variables["UMBRACO_CLOUD_IDENTITY_CLIENT_ID"] = "my-id";
        _environment.Variables["UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET"] = "my-secret";

        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string umbracoCloudJson = File.ReadAllText(Path.Combine(_directory, "src", "MySite", "umbraco-cloud.json"));
        Assert.That(umbracoCloudJson, Does.Not.Contain("my-secret").And.Contain(IdentityValues.PlaceholderClientSecret));
    }

    [Test]
    public async Task Secrets_NotConfigured_GuidesToSetTheVariablesAndPull()
    {
        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain($"1. Set the {CloudCredentialsResolver.ProjectIdVariable} and {CloudCredentialsResolver.ApiKeyVariable} environment variables"));
        Assert.That(result.Stdout, Does.Contain("umbraco cloud secrets pull").And.Contain("4. Deploy"));
        Assert.That(File.Exists(Path.Combine(_directory, LocalEnvironmentFile.FileName)), Is.False);
    }

    [Test]
    public async Task Secrets_Configured_PullsThemIntoTheRepository()
    {
        _environment.Variables[CloudCredentialsResolver.ProjectIdVariable] = "project-1";
        _environment.Variables[CloudCredentialsResolver.ApiKeyVariable] = "key-1";
        _git.Setup(x => x.GetRepositoryRootAsync(_directory, It.IsAny<CancellationToken>())).ReturnsAsync(_directory);
        _git.Setup(x => x.IsIgnoredAsync(_directory, LocalEnvironmentFile.FileName, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _cloudApi
            .Setup(x => x.GetLocalDevelopmentValuesAsync(It.Is<CloudCredentials>(credentials => credentials.ProjectId == "project-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["UMBRACO_DEPLOY_API_SECRET"] = "s3cr3t" });

        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(result.Stdout)!["secrets"]!.GetValue<string>(), Is.EqualTo("pulled"));
        Assert.That(LocalEnvironmentFile.Read(Path.Combine(_directory, LocalEnvironmentFile.FileName))["UMBRACO_DEPLOY_API_SECRET"], Is.EqualTo("s3cr3t"));
        Assert.That(result.ToString(), Does.Not.Contain("s3cr3t"));
    }

    [Test]
    public async Task Secrets_PullFails_OnlyWarns()
    {
        _environment.Variables[CloudCredentialsResolver.ProjectIdVariable] = "project-1";
        _environment.Variables[CloudCredentialsResolver.ApiKeyVariable] = "key-1";
        _cloudApi
            .Setup(x => x.GetLocalDevelopmentValuesAsync(It.IsAny<CloudCredentials>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CloudApiException(System.Net.HttpStatusCode.NotFound, "Not available."));

        CliResult result = await RunAsync("cloud", "init", "--name", "MySite", "--no-build");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Couldn't pull the local development values").And.Contain("umbraco cloud secrets pull"));
        Assert.That(result.Stdout, Does.Contain("1. Pull the local development values from Umbraco Cloud"));
    }

    private string CreateExistingProject(params (string Id, string Version)[] packages)
    {
        string project = Path.Combine(_directory, "Site.csproj");
        File.WriteAllText(project, "<Project />");
        File.WriteAllText(Path.Combine(_directory, "appsettings.json"), "{}");
        _dotnet
            .Setup(x => x.ListPackagesAsync(project, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployPackageDetectorTests.PackageList(
                topLevel: packages,
                transitive: packages.Where(package => package.Id == "Umbraco.Cms").Select(package => ("Umbraco.Cms.Core", package.Version)).ToArray()));
        return project;
    }

    /// <summary>
    /// Simulates the files the real dotnet commands create.
    /// </summary>
    private void Simulate(IReadOnlyList<string> arguments)
    {
        _commands.Add([.. arguments]);
        if (arguments is ["new", "umbraco", "-n", string name, "-o", string output, ..])
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"{name}.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(output, "Directory.Packages.props"), "<Project />");
            File.WriteAllText(Path.Combine(output, "appsettings.json"), "{\n  \"Umbraco\": {}\n}");
        }
        else if (arguments is ["new", "gitignore", "-o", string gitignoreRoot])
        {
            File.WriteAllText(Path.Combine(gitignoreRoot, ".gitignore"), "[Bb]in/\n");
        }
        else if (arguments is ["new", "sln", "-n", string solution, "-o", string root, ..])
        {
            File.WriteAllText(Path.Combine(root, $"{solution}.slnx"), "<Solution>\n</Solution>\n");
        }
    }

    private Task<CliResult> RunAsync(params string[] args) => RunAsync(new FakeTimeProvider(Now), args);

    private Task<CliResult> RunAsync(TimeProvider timeProvider, params string[] args)
        => CliRunner.RunAsync(
            _environment,
            services =>
            {
                services.AddSingleton(_dotnet.Object);
                services.AddSingleton(_prompter.Object);
                services.AddSingleton<INuGetClient>(_nuGet);
                services.AddSingleton(_cloudApi.Object);
                services.AddSingleton(_git.Object);
                services.AddSingleton(timeProvider);
            },
            args);
}
