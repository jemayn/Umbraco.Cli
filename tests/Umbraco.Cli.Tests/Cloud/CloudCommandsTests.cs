using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class CloudCommandsTests
{
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
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task Upload_File_WritesArtifactAsJson()
    {
        string zip = Path.Combine(_directory, "site.zip");
        await File.WriteAllTextAsync(zip, "zip");
        _handler.Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileName": "site.zip", "fileSize": 3 }""");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--file", zip, "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(result.Stdout)!["artifactId"]!.GetValue<string>(), Is.EqualTo("a-1"));
        Assert.That(_handler.Requests.Single().Headers[CloudApiClient.ApiKeyHeaderName], Is.EqualTo("key-1"));
    }

    [Test]
    public async Task Upload_ProjectIdOptionOverridesTheEnvironmentVariable()
    {
        string zip = Path.Combine(_directory, "site.zip");
        await File.WriteAllTextAsync(zip, "zip");
        _handler.Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileSize": 3 }""");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--file", zip, "--project-id", "project-2");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo("Artifact a-1 uploaded (3 B)."));
        RecordedRequest request = _handler.Requests.Single();
        Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-2/deployments/artifacts"));
        Assert.That(request.Headers[CloudApiClient.ApiKeyHeaderName], Is.EqualTo("key-1"));
    }

    [Test]
    public async Task Upload_ApiKeyOption_DoesNotExist()
    {
        string zip = Path.Combine(_directory, "site.zip");
        await File.WriteAllTextAsync(zip, "zip");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--file", zip, "--api-key", "key-2");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Upload_MissingApiKeyVariable_ExplainsWhereToGetIt()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ApiKeyVariable);
        string zip = Path.Combine(_directory, "site.zip");
        await File.WriteAllTextAsync(zip, "zip");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--file", zip);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain(CloudCredentialsResolver.ApiKeyVariable).And.Contain("CI/CD Flow"));
    }

    [Test]
    public async Task Upload_NotAZip_ReturnsUsageExitCode()
    {
        string file = Path.Combine(_directory, "site.tar");
        await File.WriteAllTextAsync(file, "tar");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--file", file);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Upload_SourceWithoutDotUmbraco_WarnsButUploads()
    {
        string source = Path.Combine(_directory, "src");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "Site.csproj"), "<Project />");
        _git.Setup(x => x.ListFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(["Site.csproj"]);
        _handler.Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileSize": 100 }""");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--path", source);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Contain("warning: The source code has no .umbraco file"));
    }

    [Test]
    public async Task Deploy_SourceWithoutDotUmbraco_FailsBeforeUploading()
    {
        string source = Path.Combine(_directory, "src");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "Site.csproj"), "<Project />");
        _git.Setup(x => x.ListFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(["Site.csproj"]);

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--path", source, "--environment", "dev");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("--skip-preserve-umbraco-cloud-json"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Upload_PublishedOutput_UsesTheProjectIdFromUmbracoCloudJson()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string publish = Directory.CreateDirectory(Path.Combine(_directory, "publish")).FullName;
        await File.WriteAllTextAsync(Path.Combine(publish, "Umbraco.Core.dll"), "dll");
        await File.WriteAllTextAsync(Path.Combine(publish, "umbraco-cloud.json"), """{ "Deploy": { "Project": { "Id": "project-from-file" } } }""");
        _handler.Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileSize": 100 }""");

        CliResult result = await RunAsync("cloud", "deployments", "upload", "--path", publish);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-from-file/deployments/artifacts"));
    }

    [Test]
    public async Task Upload_SourceCode_UsesTheProjectIdFromTheProjectOfDotUmbracoUnlessGiven()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string project = Directory.CreateDirectory(Path.Combine(_directory, "repo", "src", "Site")).FullName;
        await File.WriteAllTextAsync(Path.Combine(_directory, "repo", ".umbraco"), DotUmbracoFile.Create("src/Site", "Site.csproj"));
        await File.WriteAllTextAsync(Path.Combine(project, "Site.csproj"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(project, "umbraco-cloud.json"), """{ "Deploy": { "Project": { "Id": "project-from-source" } } }""");
        _git.Setup(x => x.ListFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([".umbraco", "src/Site/Site.csproj", "src/Site/umbraco-cloud.json"]);
        _handler
            .Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileSize": 100 }""")
            .Respond(HttpStatusCode.OK, """{ "artifactId": "a-2", "fileSize": 100 }""");

        CliResult fromOption = await RunAsync("cloud", "deployments", "upload", "--path", Path.Combine(_directory, "repo"), "--project-id", "project-option");
        CliResult fromFile = await RunAsync("cloud", "deployments", "upload", "--path", Path.Combine(_directory, "repo"));

        Assert.That(fromOption.ExitCode, Is.EqualTo(ExitCodes.Success), fromOption.ToString());
        Assert.That(fromFile.ExitCode, Is.EqualTo(ExitCodes.Success), fromFile.ToString());
        Assert.That(_handler.Requests.Select(request => request.Uri.AbsolutePath), Is.EqualTo(new[]
        {
            "/v2/projects/project-option/deployments/artifacts",
            "/v2/projects/project-from-source/deployments/artifacts",
        }));
    }

    [Test]
    public async Task Deploy_PathWithoutProjectId_ReturnsUsageExitCodeWithHint()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string publish = Directory.CreateDirectory(Path.Combine(_directory, "publish")).FullName;
        await File.WriteAllTextAsync(Path.Combine(publish, "Umbraco.Core.dll"), "dll");

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--path", publish, "--environment", "dev");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain($"set Deploy:Project:Id in '{publish}'"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Deploy_ArtifactIdWithConfig_UsesTheProjectIdFromTheFile()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string file = Path.Combine(_directory, "umbraco-cloud.json");
        await File.WriteAllTextAsync(file, """{ "Deploy": { "Project": { "Id": "project-from-config" } } }""");
        _handler.Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "deploymentState": "Completed" }""");

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--artifact-id", "a-1", "--environment", "live", "--config", file, "--no-wait");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-from-config/deployments"));
    }

    [Test]
    public async Task Show_ConfigFolder_UsesTheProjectIdFromItsUmbracoCloudJson()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        await File.WriteAllTextAsync(Path.Combine(_directory, "umbraco-cloud.json"), """{ "Deploy": { "Project": { "Id": "project-from-folder" } } }""");
        _handler.Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "Completed" }""");

        CliResult result = await RunAsync("cloud", "deployments", "show", "d-1", "--config", _directory);
        CliResult missing = await RunAsync("cloud", "deployments", "show", "d-1", "--config", Path.Combine(_directory, "missing.json"));

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-from-folder/deployments/d-1"));
        Assert.That(missing.ExitCode, Is.EqualTo(ExitCodes.Usage), missing.ToString());
    }

    [Test]
    public async Task Deploy_ArtifactId_StartsDeploymentAndWaitsUntilCompleted()
    {
        _git.Setup(x => x.GetHeadSubjectAsync(_directory, It.IsAny<CancellationToken>())).ReturnsAsync("Add the header");
        _handler
            .Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "environmentAlias": "stage", "deploymentState": "Pending" }""")
            .Respond(HttpStatusCode.OK, """
                {
                  "deploymentId": "d-1",
                  "environmentAlias": "stage",
                  "deploymentState": "Completed",
                  "createdUtc": "2026-09-30T12:00:00Z",
                  "completedUtc": "2026-09-30T12:06:12Z",
                  "deploymentStatusMessages": [ { "message": "Done", "timestampUtc": "2026-09-30T12:06:12Z" } ]
                }
                """);

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--artifact-id", "a-1", "--environment", "stage", "--no-build-and-restore");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo("Deployment d-1 to stage: Completed in 6m 12s."));
        Assert.That(result.Stderr, Does.Contain("Done"));
        JsonNode body = JsonNode.Parse(_handler.Requests[0].BodyText)!;
        Assert.That(body["commitMessage"]!.GetValue<string>(), Is.EqualTo("Add the header"));
        Assert.That(body["noBuildAndRestore"]!.GetValue<bool>(), Is.True);
        Assert.That(_handler.Requests[1].Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-1/deployments/d-1"));
    }

    [Test]
    public async Task Deploy_Failed_ReturnsDeploymentFailedExitCode()
    {
        _handler
            .Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "deploymentState": "Queued" }""")
            .Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "Failed" }""");

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--artifact-id", "a-1", "--environment", "stage", "-m", "Try", "--verbose");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.DeploymentFailed), result.ToString());
        Assert.That(result.Stderr, Does.Contain("error: Deployment d-1 failed."));
    }

    [Test]
    public async Task Deploy_NoWait_ReturnsAfterStarting()
    {
        _handler.Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "deploymentState": "Pending" }""");

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--artifact-id", "a-1", "--environment", "stage", "--no-wait", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(result.Stdout)!["deploymentState"]!.GetValue<string>(), Is.EqualTo("Pending"));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
        _git.Verify(x => x.GetHeadSubjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Deploy_File_UploadsFirstAndIncludesArtifactIdInJson()
    {
        string zip = Path.Combine(_directory, "site.zip");
        await File.WriteAllTextAsync(zip, "zip");
        _handler
            .Respond(HttpStatusCode.OK, """{ "artifactId": "a-9", "fileSize": 3 }""")
            .Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "deploymentState": "Pending" }""");

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--file", zip, "--environment", "dev", "--no-wait", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(result.Stdout)!["artifactId"]!.GetValue<string>(), Is.EqualTo("a-9"));
        Assert.That(result.Stderr, Does.Contain("Artifact a-9 uploaded."));
        Assert.That(JsonNode.Parse(_handler.Requests[1].BodyText)!["artifactId"]!.GetValue<string>(), Is.EqualTo("a-9"));
        Assert.That(JsonNode.Parse(_handler.Requests[1].BodyText)!["commitMessage"]!.GetValue<string>(), Is.EqualTo(DeployCommandHandler.DefaultCommitMessage));
    }

    [Test]
    public async Task Deploy_File_TextResultIncludesTheArtifactId()
    {
        string zip = Path.Combine(_directory, "site.zip");
        await File.WriteAllTextAsync(zip, "zip");
        _handler
            .Respond(HttpStatusCode.OK, """{ "artifactId": "a-9", "fileSize": 3 }""")
            .Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "environmentAlias": "dev", "deploymentState": "Pending" }""");

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--file", zip, "--environment", "dev", "--no-wait");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo("Deployment d-1 of artifact a-9 to dev: Pending."));
    }

    [TestCase("--artifact-id", "a-1", "--file", "x.zip")]
    [TestCase("--file", "x.zip", "--path", ".")]
    public async Task Deploy_SeveralSources_ReturnsUsageExitCode(params string[] sourceArguments)
    {
        CliResult result = await RunAsync(["cloud", "deployments", "deploy", "--environment", "dev", .. sourceArguments]);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Specify only one of --artifact-id, --file or --path."));
    }

    [Test]
    public async Task Deploy_NoSourceOutsideAGitDeploymentRepository_ExplainsHowToPublish()
    {
        string repository = CreateRepository(dotUmbraco: false);
        _environment.CurrentDirectory = repository;

        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--environment", "dev");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("No artifact to upload was specified.").And.Contain("dotnet publish --output publish"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Upload_NoSourceInAGitDeploymentRepository_UploadsTheRepositoryWithTheProjectIdOfItsProject()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string repository = CreateRepository(dotUmbraco: true);
        _environment.CurrentDirectory = Path.Combine(repository, "src");
        _git.Setup(x => x.ListFilesAsync(repository, It.IsAny<CancellationToken>())).ReturnsAsync([".umbraco", "src/Site/Site.csproj", "src/Site/umbraco-cloud.json"]);
        _handler.Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileSize": 100 }""");

        CliResult result = await RunAsync("cloud", "deployments", "upload");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Contain($"Zipped '{repository}' for a Git deployment"));
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-from-repository/deployments/artifacts"));
    }

    [Test]
    public async Task Upload_TypeWithFile_ReturnsUsageExitCode()
    {
        CliResult result = await RunAsync("cloud", "deployments", "upload", "--file", "x.zip", "--type", "git");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("--type can't be used with --file."));
    }

    [Test]
    public async Task Show_FromTheRootOfAnArtifactDeploymentRepository_FindsTheProjectId()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string repository = CreateRepository(dotUmbraco: false);

        // Published output has a copy of umbraco-cloud.json, but no project file
        string publish = Directory.CreateDirectory(Path.Combine(repository, "publish")).FullName;
        File.Copy(Path.Combine(repository, "src", "Site", "umbraco-cloud.json"), Path.Combine(publish, "umbraco-cloud.json"));
        _environment.CurrentDirectory = repository;
        _handler.Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "Completed" }""");

        CliResult result = await RunAsync("cloud", "deployments", "show", "d-1");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-from-repository/deployments/d-1"));
    }

    [Test]
    public async Task Show_SeveralUmbracoProjectsInTheRepository_AsksToChoose()
    {
        _environment.Variables.Remove(CloudCredentialsResolver.ProjectIdVariable);
        string repository = CreateRepository(dotUmbraco: false);
        string other = Directory.CreateDirectory(Path.Combine(repository, "src", "Other")).FullName;
        await File.WriteAllTextAsync(Path.Combine(other, "Other.csproj"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(other, "umbraco-cloud.json"), """{ "Deploy": { "Project": { "Id": "other-project" } } }""");
        _environment.CurrentDirectory = repository;

        CliResult result = await RunAsync("cloud", "deployments", "show", "d-1");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Several Umbraco projects"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [TestCase(null, "/v2/projects/project-1/deployments?skip=0&take=1")]
    [TestCase("live", "/v2/projects/project-1/deployments?skip=0&take=1&targetEnvironmentAlias=live")]
    public async Task Show_NoDeploymentId_ShowsTheLatestDeployment(string? environment, string listPath)
    {
        _handler
            .Respond(HttpStatusCode.OK, """{ "projectId": "project-1", "data": [{ "id": "d-9", "state": "Completed" }], "totalItems": 3 }""")
            .Respond(HttpStatusCode.OK, """{ "deploymentId": "d-9", "deploymentState": "Completed" }""");

        CliResult result = await RunAsync(["cloud", "deployments", "show", .. environment is null ? Array.Empty<string>() : ["-e", environment]]);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo("Deployment d-9: Completed."));
        Assert.That(_handler.Requests.Select(request => request.Uri.PathAndQuery), Is.EqualTo(new[] { listPath, "/v2/projects/project-1/deployments/d-9" }));
    }

    [Test]
    public async Task Show_NoDeploymentIdWithoutDeployments_Fails()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "projectId": "project-1", "data": [], "totalItems": 0 }""");

        CliResult result = await RunAsync("cloud", "deployments", "show");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Error), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Umbraco Cloud has no deployments for this project."));
    }

    [Test]
    public async Task Show_DeploymentIdWithEnvironment_ReturnsUsageExitCode()
    {
        CliResult result = await RunAsync("cloud", "deployments", "show", "d-1", "-e", "live");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Deploy_InvalidTimeout_ReturnsUsageExitCode()
    {
        CliResult result = await RunAsync("cloud", "deployments", "deploy", "--artifact-id", "a-1", "--environment", "dev", "--timeout", "soon");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("'soon' is not a valid duration"));
    }

    [Test]
    public async Task Show_WritesDeploymentState()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "InProgress" }""");

        CliResult result = await RunAsync("cloud", "deployments", "show", "d-1");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo("Deployment d-1: InProgress."));
    }

    [Test]
    public async Task Show_Unauthorized_ReturnsAuthenticationExitCode()
    {
        _handler.Respond(HttpStatusCode.Unauthorized);

        CliResult result = await RunAsync("cloud", "deployments", "show", "d-1");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.AuthenticationFailed), result.ToString());
        Assert.That(result.Stderr, Does.Not.Contain("key-1"));
    }

    /// <summary>
    /// Creates a git repository with an Umbraco project in <c>src/Site</c> (project ID <c>project-from-repository</c>), and a <c>.umbraco</c> file for a Git deployment.
    /// </summary>
    private string CreateRepository(bool dotUmbraco)
    {
        string repository = Directory.CreateDirectory(Path.Combine(_directory, "repository")).FullName;
        Directory.CreateDirectory(Path.Combine(repository, ".git"));
        string project = Directory.CreateDirectory(Path.Combine(repository, "src", "Site")).FullName;
        File.WriteAllText(Path.Combine(project, "Site.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(project, "umbraco-cloud.json"), """{ "Deploy": { "Project": { "Id": "project-from-repository" } } }""");
        if (dotUmbraco)
        {
            File.WriteAllText(Path.Combine(repository, ".umbraco"), DotUmbracoFile.Create("src/Site", "Site.csproj"));
        }

        return repository;
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
