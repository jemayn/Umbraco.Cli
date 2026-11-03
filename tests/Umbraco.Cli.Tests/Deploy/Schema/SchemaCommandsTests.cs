using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cli.Deploy.Schema;
using Umbraco.Cli.Deploy.Schema.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Deploy.Schema;

[TestFixture]
public class SchemaCommandsTests
{
    private const string Secret = "c2VjcmV0LXZhbHVlLWZvci10ZXN0cw==";
    private static readonly Guid TaskId = Guid.Parse("8c327019-20bb-4b49-b514-386415648981");

    private FakeEnvironment _environment = null!;
    private FakeHttpMessageHandler _handler = null!;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _environment = new FakeEnvironment(_directory);
        _handler = new FakeHttpMessageHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task Deploy_StartsAndWaitsUsingTheUmbracoCloudJson()
    {
        string publish = Directory.CreateDirectory(Path.Combine(_directory, "publish")).FullName;
        await File.WriteAllTextAsync(Path.Combine(publish, "umbraco-cloud.json"), $$"""
            { "Deploy": { "Settings": { "ApiSecret": "{{Secret}}" }, "Project": { "Workspaces": [ { "Name": "Live", "Type": "live", "Url": "https://live.example.com" } ] } } }
            """);
        _handler
            .Respond(HttpStatusCode.OK, Started())
            .Respond(HttpStatusCode.OK, Status("Completed", """, "Comment": "Completed", "Percent": 100"""));

        CliResult result = await RunAsync("deploy", "schema", "deploy", "--config", "publish", "-e", "live");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo($"Schema deployment {TaskId} to live: Completed."));
        Assert.That(result.Stderr, Does.Contain($"Schema deployment {TaskId} to live started."));
        Assert.That(_handler.Requests.Select(request => request.Uri.AbsoluteUri), Is.EqualTo(new[]
        {
            "https://live.example.com/umbraco/umbracodeploy/extract/start/Umbraco%20CLI",
            $"https://live.example.com/umbraco/umbracodeploy/statusreport/get/{TaskId}",
        }));
        Assert.That(result.ToString(), Does.Not.Contain(Secret));
    }

    [Test]
    public async Task Deploy_Failed_WritesJsonAndReturnsDeploymentFailed()
    {
        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = Secret;
        _handler
            .Respond(HttpStatusCode.OK, Started())
            .Respond(HttpStatusCode.OK, Status("Failed", """, "Log": "The log", "Exception": { "Type": "System.Exception", "Detail": "Boom." }"""));

        CliResult result = await RunAsync("deploy", "schema", "deploy", "--url", "https://example.com", "--reason", "Pipeline 42", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.DeploymentFailed), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["taskId"]!.GetValue<string>(), Is.EqualTo(TaskId.ToString()));
        Assert.That(json["url"]!.GetValue<string>(), Is.EqualTo("https://example.com"));
        Assert.That(json["status"]!.GetValue<string>(), Is.EqualTo("Failed"));
        Assert.That(json["exception"]!["detail"]!.GetValue<string>(), Is.EqualTo("Boom."));
        Assert.That(json["log"]!.GetValue<string>(), Is.EqualTo("The log"));
        Assert.That(json["restarted"]!.GetValue<bool>(), Is.False);
        Assert.That(result.Stderr, Does.Contain($"error: Schema deployment {TaskId} to https://example.com failed: Boom."));
        Assert.That(_handler.Requests[0].Uri.AbsolutePath, Does.EndWith("/extract/start/Pipeline%2042"));
    }

    [Test]
    public async Task Deploy_JoinsARunningSchemaDeployment()
    {
        _handler
            .Respond(HttpStatusCode.OK, $$"""{ "TaskId": "{{TaskId}}", "TaskStarted": false }""")
            .Respond(HttpStatusCode.OK, Status("Completed"));

        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = Secret;

        CliResult result = await RunAsync("deploy", "schema", "deploy", "--url", "https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Contain($"Joined schema deployment {TaskId} on https://example.com"));
        Assert.That(_handler.Requests[1].Uri.AbsolutePath, Is.EqualTo($"/umbraco/umbracodeploy/statusreport/get/{TaskId}"));
    }

    [Test]
    public async Task Deploy_NoWait_ReturnsAfterTheStart()
    {
        _handler.Respond(HttpStatusCode.OK, Started());

        _environment.Variables[SchemaTargetResolver.ApiKeyVariable] = "legacy-key";

        CliResult result = await RunAsync("deploy", "schema", "deploy", "--url", "https://example.com", "--no-wait");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo($"Schema deployment {TaskId} to https://example.com started."));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Deploy_Unauthorized_ReturnsAuthenticationFailed()
    {
        _handler.Respond(HttpStatusCode.Unauthorized);

        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = Secret;

        CliResult result = await RunAsync("deploy", "schema", "deploy", "--url", "https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.AuthenticationFailed), result.ToString());
        Assert.That(result.Stderr, Does.Contain("rejected the credentials").And.Contain(SchemaTargetResolver.ApiSecretVariable));
        Assert.That(result.ToString(), Does.Not.Contain(Secret));
    }

    [TestCase("--url", "https://example.com", "-e", "Live")]
    [TestCase("--url", "https://example.com", "--api-key", "k", "--api-secret", Secret)]
    [TestCase("--url", "https://example.com", "--reason", "a/b")]
    [TestCase("--url", "https://example.com", "--timeout", "soon")]
    public async Task Deploy_InvalidOptions_ReturnsUsageExitCode(params string[] options)
    {
        CliResult result = await RunAsync(["deploy", "schema", "deploy", .. options]);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Deploy_NoCredentials_ReturnsUsageExitCode()
    {
        CliResult result = await RunAsync("deploy", "schema", "deploy", "--url", "https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("No ApiSecret for Umbraco Deploy was specified."));
    }

    [Test]
    public async Task Show_WithoutTaskId_GetsTheCurrentSchemaDeployment()
    {
        _environment.Variables[SchemaTargetResolver.ApiKeyVariable] = "legacy-key";
        _handler.Respond(HttpStatusCode.OK, Status("Executing", """, "Comment": "Processing", "Percent": 40"""));

        CliResult result = await RunAsync("deploy", "schema", "show", "--url", "https://example.com", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["status"]!.GetValue<string>(), Is.EqualTo("Executing"));
        Assert.That(json["percent"]!.GetValue<int>(), Is.EqualTo(40));
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/umbraco/umbracodeploy/statusreport/getcurrent"));
    }

    [Test]
    public async Task Show_UnknownTask_ReturnsError()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""{ "Id": "{{TaskId}}", "Status": "Unknown", "Comment": "No work item found with the specified Id", "Percent": 0 }""");

        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = Secret;

        CliResult result = await RunAsync("deploy", "schema", "show", TaskId.ToString(), "--url", "https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Error), result.ToString());
        Assert.That(result.Stderr, Does.Contain($"doesn't know the schema deployment {TaskId}"));
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo($"/umbraco/umbracodeploy/statusreport/get/{TaskId}"));
    }

    [Test]
    public async Task Show_Wait_WaitsUntilCompleted()
    {
        _handler
            .Respond(HttpStatusCode.OK, Status("Executing"))
            .Respond(HttpStatusCode.OK, Status("Completed"));

        _environment.Variables[SchemaTargetResolver.ApiSecretVariable] = Secret;

        CliResult result = await RunAsync("deploy", "schema", "show", TaskId.ToString(), "--wait", "--url", "https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout.Trim(), Is.EqualTo($"Schema deployment {TaskId} to https://example.com: Completed."));
    }

    private static string Started() => $$"""{ "TaskId": "{{TaskId}}", "TaskStarted": true }""";

    private static string Status(string status, string extra = "")
        => $$"""{ "Id": "{{TaskId}}", "Status": "{{status}}", "Percent": -1{{extra}} }""";

    private Task<CliResult> RunAsync(params string[] args)
        => CliRunner.RunAsync(
            _environment,
            services => services.AddHttpClient<IDeployApiClient, DeployApiClient>().ConfigurePrimaryHttpMessageHandler(() => _handler),
            args);
}
