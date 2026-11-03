using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Deploy.Schema;
using Umbraco.Cli.Deploy.Schema.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Deploy.Schema;

[TestFixture]
public class DeployApiClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("secret-key");
    private static readonly Guid TaskId = Guid.Parse("8c327019-20bb-4b49-b514-386415648981");

    private FakeHttpMessageHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = new FakeHttpMessageHandler();

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task StartAsync_PostsTheEscapedReasonWithAValidToken()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""{ "TaskId": "{{TaskId}}", "TaskStarted": true }""");

        ExtractStartResponse response = await CreateClient().StartAsync(Target("https://example.com/site/"), "Umbraco CLI", CancellationToken.None);

        Assert.That(response.TaskId, Is.EqualTo(TaskId));
        Assert.That(response.TaskStarted, Is.True);
        RecordedRequest request = _handler.Requests.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Uri.AbsoluteUri, Is.EqualTo("https://example.com/site/umbraco/umbracodeploy/extract/start/Umbraco%20CLI"));
        AssertValidToken(request);
    }

    [Test]
    public async Task GetStatusAsync_ReadsTheStatusReport()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""
            {
              "Id": "{{TaskId}}", "Status": "Failed", "Comment": "Failed", "Percent": 100, "IsHeartbeat": false, "Log": "line 1",
              "Exception": { "Type": "System.Exception", "Detail": "Boom", "StackTrace": null, "InnerException": null },
              "MismatchList": [ { "Udi": "umb://document-type/1", "Name": "Home", "Alias": "home", "Differences": [] } ],
              "Type": "Umbraco.Deploy.Infrastructure.Work.DiskReadWorkItem, Umbraco.Deploy.Infrastructure"
            }
            """);

        WorkStatusResponse status = await CreateClient().GetStatusAsync(Target("https://example.com"), TaskId, CancellationToken.None);

        Assert.That(status.Id, Is.EqualTo(TaskId));
        Assert.That(status.IsFailed, Is.True);
        Assert.That(status.Exception!.Detail, Is.EqualTo("Boom"));
        Assert.That(status.Log, Is.EqualTo("line 1"));
        Assert.That(status.MismatchList!.Single().Udi, Is.EqualTo("umb://document-type/1"));
        RecordedRequest request = _handler.Requests.Single();
        Assert.That(request.Uri.AbsolutePath, Is.EqualTo($"/umbraco/umbracodeploy/statusreport/get/{TaskId}"));
        AssertValidToken(request);
    }

    [Test]
    public async Task GetStatusAsync_WithoutTaskId_GetsTheCurrentTask()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "Id": "00000000-0000-0000-0000-000000000000", "Status": "Unknown", "Comment": "No existing work item", "Percent": 0 }""");

        WorkStatusResponse status = await CreateClient().GetStatusAsync(Target("https://example.com"), null, CancellationToken.None);

        Assert.That(status.IsKnown, Is.False);
        Assert.That(status.IsTerminal, Is.False);
        Assert.That(_handler.Requests.Single().Uri.AbsolutePath, Is.EqualTo("/umbraco/umbracodeploy/statusreport/getcurrent"));
    }

    [Test]
    public void Unauthorized_ThrowsWithAuthenticationExitCodeAndHint()
    {
        _handler.Respond(HttpStatusCode.Unauthorized, """{ "title": "Unauthorized", "detail": "Authorization token is invalid." }""");

        DeployApiException exception = Assert.ThrowsAsync<DeployApiException>(() => CreateClient().GetStatusAsync(Target("https://example.com"), null, CancellationToken.None))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.AuthenticationFailed));
        Assert.That(exception.Message, Does.Contain("rejected the credentials"));
        Assert.That(exception.Hint, Does.Contain("UMBRACO_DEPLOY_API_SECRET").And.Contain("20 minutes"));
        Assert.That(exception.IsTransient, Is.False);
    }

    [TestCase(HttpStatusCode.Forbidden, "403 Forbidden", false)]
    [TestCase(HttpStatusCode.NotFound, "wasn't found at https://example.com", false)]
    [TestCase(HttpStatusCode.InternalServerError, "responded 500", true)]
    [TestCase(HttpStatusCode.TooManyRequests, "responded 429", true)]
    public void ErrorResponse_ThrowsDeployApiException(HttpStatusCode statusCode, string expected, bool transient)
    {
        _handler.Respond(statusCode);

        DeployApiException exception = Assert.ThrowsAsync<DeployApiException>(() => CreateClient().GetStatusAsync(Target("https://example.com"), TaskId, CancellationToken.None))!;

        Assert.That(exception.Message, Does.Contain(expected));
        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.Error));
        Assert.That(exception.IsTransient, Is.EqualTo(transient));
    }

    [Test]
    public void NetworkError_IsTransientAndNamesTheUrl()
    {
        _handler.Throw(new HttpRequestException("No such host is known."));

        DeployApiException exception = Assert.ThrowsAsync<DeployApiException>(() => CreateClient().GetStatusAsync(Target("https://example.com"), TaskId, CancellationToken.None))!;

        Assert.That(exception.Message, Is.EqualTo("Could not reach https://example.com: No such host is known."));
        Assert.That(exception.IsTransient, Is.True);
    }

    [Test]
    public void StartAsync_Timeout_IsNotRetriedAndSaysItMayHaveStarted()
    {
        _handler.Hang();

        CliException exception = Assert.ThrowsAsync<CliException>(() => CreateClient().StartAsync(Target("https://example.com"), "Umbraco CLI", CancellationToken.None))!;

        Assert.That(exception, Is.Not.InstanceOf<DeployApiException>());
        Assert.That(exception.Message, Does.Contain("may have started anyway"));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public void UnexpectedResponse_ThrowsCliException()
    {
        _handler.Respond(HttpStatusCode.OK, "<html>Login</html>", response => response.Content.Headers.ContentType = new("application/json"));

        CliException exception = Assert.ThrowsAsync<CliException>(() => CreateClient().GetStatusAsync(Target("https://example.com"), TaskId, CancellationToken.None))!;

        Assert.That(exception.Message, Does.Contain("unexpected response"));
    }

    private static SchemaTarget Target(string url) => new(new Uri(url.TrimEnd('/') + "/"), null, new DeployCredentials(Key, "UMBRACO_DEPLOY_API_SECRET"));

    private static void AssertValidToken(RecordedRequest request)
    {
        Assert.That(request.Headers["Authorization"], Does.StartWith("Bearer "));
        string[] parts = Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers["Authorization"]["Bearer ".Length..])).Split(':');
        Assert.That(parts, Has.Length.EqualTo(3));
        Assert.That(parts[2], Is.EqualTo(Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));

        // As Umbraco Deploy validates it: the HMAC of the path and query, timestamp and nonce
        string expected = Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(request.Uri.PathAndQuery + parts[2] + parts[1])));
        Assert.That(parts[0], Is.EqualTo(expected));
    }

    private DeployApiClient CreateClient()
        => new(new HttpClient(_handler, disposeHandler: false), new FakeTimeProvider(Now), Mock.Of<IReporter>()) { RequestTimeout = TimeSpan.FromMilliseconds(50) };
}
