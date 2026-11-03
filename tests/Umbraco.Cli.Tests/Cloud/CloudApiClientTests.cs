using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class CloudApiClientTests
{
    private static readonly CloudCredentials Credentials = new("project-1", "secret-key");

    private FakeHttpMessageHandler _handler = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeHttpMessageHandler();
        _timeProvider = new FakeTimeProvider();
    }

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task UploadArtifactAsync_PostsMultipartFormWithApiKeyHeader()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        await File.WriteAllTextAsync(zipPath, "zip-content");
        _handler.Respond(HttpStatusCode.OK, """{ "artifactId": "a-1", "fileName": "site.zip", "fileSize": 11 }""");
        var reported = new List<long>();

        try
        {
            ArtifactResponse artifact = await CreateClient().UploadArtifactAsync(Credentials, zipPath, "My site", "1.2.3", new CallbackProgress(reported.Add), CancellationToken.None);

            Assert.That(artifact.ArtifactId, Is.EqualTo("a-1"));
            RecordedRequest request = _handler.Requests.Single();
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Uri.ToString(), Is.EqualTo("https://api.test/v2/projects/project-1/deployments/artifacts"));
            Assert.That(request.Headers[CloudApiClient.ApiKeyHeaderName], Is.EqualTo("secret-key"));
            Assert.That(request.ContentType, Is.EqualTo("multipart/form-data"));
            Assert.That(request.BodyText, Does.Contain("name=file").And.Contain("zip-content").And.Contain("name=description").And.Contain("My site").And.Contain("name=version").And.Contain("1.2.3"));
            Assert.That(reported.Last(), Is.EqualTo(11));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task StartDeploymentAsync_PostsCamelCaseBody()
    {
        _handler.Respond(HttpStatusCode.Created, """{ "deploymentId": "d-1", "deploymentState": "Pending" }""");

        DeploymentResponse deployment = await CreateClient().StartDeploymentAsync(
            Credentials,
            new StartDeploymentRequest { ArtifactId = "a-1", TargetEnvironmentAlias = "stage", CommitMessage = "Ship it", SkipVersionCheck = true },
            CancellationToken.None);

        Assert.That(deployment.DeploymentId, Is.EqualTo("d-1"));
        JsonNode body = JsonNode.Parse(_handler.Requests.Single().BodyText)!;
        Assert.That(body["artifactId"]!.GetValue<string>(), Is.EqualTo("a-1"));
        Assert.That(body["targetEnvironmentAlias"]!.GetValue<string>(), Is.EqualTo("stage"));
        Assert.That(body["commitMessage"]!.GetValue<string>(), Is.EqualTo("Ship it"));
        Assert.That(body["skipVersionCheck"]!.GetValue<bool>(), Is.True);
        Assert.That(body["noBuildAndRestore"]!.GetValue<bool>(), Is.False);
        Assert.That(body["skipPreserveUmbracoCloudJson"]!.GetValue<bool>(), Is.False);
        Assert.That(body["runSchemaExtraction"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void StartDeploymentAsync_Conflict_ExplainsAnotherDeploymentIsRunning()
    {
        _handler.Respond(HttpStatusCode.Conflict, """{ "title": "Conflict", "detail": "A deployment is already running." }""");

        CloudApiException exception = Assert.ThrowsAsync<CloudApiException>(() => CreateClient().StartDeploymentAsync(
            Credentials,
            new StartDeploymentRequest { ArtifactId = "a", TargetEnvironmentAlias = "dev", CommitMessage = "m" },
            CancellationToken.None))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.Error));
        Assert.That(exception.Message, Does.Contain("another deployment is in progress").And.Contain("A deployment is already running"));
    }

    [Test]
    public void StartDeploymentAsync_Timeout_IsAnErrorThatMayHaveStartedTheDeployment()
    {
        _handler.Hang();

        CliException exception = Assert.ThrowsAsync<CliException>(() => new CloudApiClient(CreateHttpClient(), _timeProvider, Mock.Of<IReporter>()) { RequestTimeout = TimeSpan.FromMilliseconds(50) }.StartDeploymentAsync(
            Credentials,
            new StartDeploymentRequest { ArtifactId = "a", TargetEnvironmentAlias = "dev", CommitMessage = "m" },
            CancellationToken.None))!;

        Assert.That(exception, Is.Not.InstanceOf<OperationCanceledException>());
        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.Error));
        Assert.That(exception.Message, Does.Contain("may have started"));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetDeploymentAsync_ClampsLongRetryAfter()
    {
        _handler
            .Respond(HttpStatusCode.TooManyRequests, configure: response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(1)))
            .Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "Completed" }""");

        DeploymentResponse deployment = await TimeAdvancer.RunAsync(
            CreateClient().GetDeploymentAsync(Credentials, "d-1", null, CancellationToken.None),
            _timeProvider,
            TimeSpan.FromSeconds(1),
            maxSteps: 70);

        Assert.That(deployment.IsCompleted, Is.True);
    }

    [Test]
    public void Unauthorized_MapsToAuthenticationExitCode()
    {
        _handler.Respond(HttpStatusCode.Unauthorized);

        CloudApiException exception = Assert.ThrowsAsync<CloudApiException>(() => CreateClient().GetDeploymentAsync(Credentials, "d-1", null, CancellationToken.None))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.AuthenticationFailed));
        Assert.That(exception.Hint, Does.Contain("UMBRACO_CLOUD_API_KEY"));
    }

    [Test]
    public void BadRequest_IncludesProblemDetail()
    {
        _handler.Respond(HttpStatusCode.BadRequest, """{ "title": "Bad Request", "detail": "Invalid target environment." }""");

        CloudApiException exception = Assert.ThrowsAsync<CloudApiException>(() => CreateClient().GetDeploymentAsync(Credentials, "d-1", null, CancellationToken.None))!;

        Assert.That(exception.Message, Is.EqualTo("Umbraco Cloud responded 400 Bad Request (Invalid target environment)."));
    }

    [Test]
    public async Task GetDeploymentAsync_PassesLastModifiedUtc()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "InProgress" }""");

        await CreateClient().GetDeploymentAsync(Credentials, "d-1", new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.That(_handler.Requests.Single().Uri.Query, Is.EqualTo("?lastModifiedUtc=2026-09-30T12%3A00%3A00.0000000Z"));
    }

    [Test]
    public async Task GetDeploymentAsync_RetriesTransientFailuresWithBackoff()
    {
        _handler
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Throw(new HttpRequestException("Connection reset"))
            .Respond(HttpStatusCode.TooManyRequests, configure: response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3)))
            .Respond(HttpStatusCode.OK, """{ "deploymentId": "d-1", "deploymentState": "Completed" }""");

        DeploymentResponse deployment = await TimeAdvancer.RunAsync(
            CreateClient().GetDeploymentAsync(Credentials, "d-1", null, CancellationToken.None),
            _timeProvider,
            TimeSpan.FromSeconds(1));

        Assert.That(deployment.IsCompleted, Is.True);
        Assert.That(_handler.Requests, Has.Count.EqualTo(4));
    }

    [Test]
    public void GetDeploymentAsync_GivesUpAfterMaxAttempts()
    {
        for (int i = 0; i < CloudApiClient.MaxPollAttempts; i++)
        {
            _handler.Respond(HttpStatusCode.BadGateway);
        }

        CloudApiException exception = Assert.ThrowsAsync<CloudApiException>(() => TimeAdvancer.RunAsync(
            CreateClient().GetDeploymentAsync(Credentials, "d-1", null, CancellationToken.None),
            _timeProvider,
            TimeSpan.FromSeconds(5)))!;

        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(_handler.Requests, Has.Count.EqualTo(CloudApiClient.MaxPollAttempts));
    }

    [Test]
    public async Task GetDeploymentAsync_MissingFields_KeepDefaults()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        DeploymentResponse deployment = await CreateClient().GetDeploymentAsync(Credentials, "d-1", null, CancellationToken.None);

        Assert.That(deployment.DeploymentStatusMessages, Is.Empty);
        Assert.That(deployment.DeploymentState, Is.Empty);
        Assert.That(deployment.DeploymentId, Is.Empty);
    }

    [Test]
    public async Task GetLocalDevelopmentValuesAsync_GetsTheValuesWithApiKeyHeader()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "values": { "UMBRACO_DEPLOY_API_SECRET": "s3cr3t" } }""");

        IReadOnlyDictionary<string, string> values = await CreateClient().GetLocalDevelopmentValuesAsync(Credentials, CancellationToken.None);

        Assert.That(values, Is.EquivalentTo(new Dictionary<string, string> { ["UMBRACO_DEPLOY_API_SECRET"] = "s3cr3t" }));
        RecordedRequest request = _handler.Requests.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/v2/projects/project-1/local-development/values"));
        Assert.That(request.Headers[CloudApiClient.ApiKeyHeaderName], Is.EqualTo("secret-key"));
    }

    [Test]
    public void GetLocalDevelopmentValuesAsync_NotFound_ThrowsCloudApiException()
    {
        _handler.Respond(HttpStatusCode.NotFound);

        CloudApiException exception = Assert.ThrowsAsync<CloudApiException>(() => CreateClient().GetLocalDevelopmentValuesAsync(Credentials, CancellationToken.None))!;

        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(exception.Message, Does.Contain("local development values"));
    }

    [TestCase(1, 5)]
    [TestCase(2, 10)]
    [TestCase(3, 20)]
    [TestCase(4, 25)]
    [TestCase(9, 25)]
    public void GetBackoff_DoublesUpToThePollInterval(int attempt, int expectedSeconds)
        => Assert.That(CloudApiClient.GetBackoff(attempt), Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));

    [TestCase(null, "https://api.cloud.umbraco.com/")]
    [TestCase("", "https://api.cloud.umbraco.com/")]
    [TestCase("https://api.example.com", "https://api.example.com/")]
    [TestCase("http://localhost:5000/", "http://localhost:5000/")]
    public void GetBaseAddress_AcceptsHttpsAndLocalHttp(string? url, string expected)
        => Assert.That(CloudApiClient.GetBaseAddress(url, "VARIABLE").ToString(), Is.EqualTo(expected));

    [TestCase("http://api.example.com")]
    [TestCase("ftp://api.example.com")]
    [TestCase("not a url")]
    public void GetBaseAddress_RefusesUrlsThatWouldExposeTheApiKey(string url)
        => Assert.That(() => CloudApiClient.GetBaseAddress(url, "VARIABLE"), Throws.TypeOf<UsageException>().With.Message.Contains("VARIABLE"));

    private CloudApiClient CreateClient() => new(CreateHttpClient(), _timeProvider, Mock.Of<IReporter>());

    private HttpClient CreateHttpClient() => new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") };

    private sealed class CallbackProgress : IProgress<long>
    {
        private readonly Action<long> _callback;

        public CallbackProgress(Action<long> callback) => _callback = callback;

        public void Report(long value) => _callback(value);
    }
}
