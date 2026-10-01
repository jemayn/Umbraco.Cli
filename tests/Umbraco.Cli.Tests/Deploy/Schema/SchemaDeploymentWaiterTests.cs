using System.Net;
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
public class SchemaDeploymentWaiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly SchemaTarget Target = new(new Uri("https://live.example.com/"), "Live", new DeployCredentials([1, 2, 3], "--api-secret"));
    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private FakeTimeProvider _timeProvider = null!;
    private Mock<IDeployApiClient> _api = null!;
    private StringWriter _stderr = null!;

    [SetUp]
    public void SetUp()
    {
        _timeProvider = new FakeTimeProvider(Start);
        _timeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);
        _api = new Mock<IDeployApiClient>(MockBehavior.Strict);
        _stderr = new StringWriter { NewLine = "\n" };
    }

    [TearDown]
    public void TearDown() => _stderr.Dispose();

    [Test]
    public async Task WaitAsync_PollsUntilCompletedAndPrintsTheProgress()
    {
        _api.SetupSequence(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(First, WorkStatuses.Unknown))
            .ThrowsAsync(new DeployApiException(HttpStatusCode.InternalServerError, "Umbraco Deploy on Live responded 500 Internal Server Error."))
            .ReturnsAsync(Status(First, WorkStatuses.Executing, "Processing", 10))
            .ReturnsAsync(Status(First, WorkStatuses.Executing, "Processing", 10))
            .ReturnsAsync(Status(First, WorkStatuses.Completed, "Completed", 100));

        SchemaWaitResult result = await RunAsync(CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), restart: null, CancellationToken.None));

        Assert.That(result.TaskId, Is.EqualTo(First));
        Assert.That(result.Status.IsCompleted, Is.True);
        Assert.That(result.Restarted, Is.False);
        Assert.That(Lines(), Is.EqualTo(new[]
        {
            $"Waiting for schema deployment {First}",
            "State: Executing",
            "Processing (10%)",
            "State: Completed",
            "Completed (100%)",
        }));
    }

    [Test]
    public async Task WaitAsync_ReturnsAFailedStatus()
    {
        _api.Setup(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>())).ReturnsAsync(Status(First, "Mismatch"));

        SchemaWaitResult result = await CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), restart: null, CancellationToken.None);

        Assert.That(result.Status.IsFailed, Is.True);
    }

    [Test]
    public async Task WaitAsync_UnknownBeyondTheGracePeriod_StartsAgainOnce()
    {
        _api.Setup(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>())).ReturnsAsync(Status(First, WorkStatuses.Unknown));
        _api.SetupSequence(x => x.GetStatusAsync(Target, Second, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(Second, WorkStatuses.Executing))
            .ReturnsAsync(Status(Second, WorkStatuses.Completed));
        int restarts = 0;

        SchemaWaitResult result = await RunAsync(CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), _ => { restarts++; return Task.FromResult(Second); }, CancellationToken.None));

        Assert.That(restarts, Is.EqualTo(1));
        Assert.That(result.TaskId, Is.EqualTo(Second));
        Assert.That(result.Restarted, Is.True);
        Assert.That(result.Status.IsCompleted, Is.True);
        Assert.That(_stderr.ToString(), Does.Contain($"Schema deployment {First} is no longer known").And.Contain($"started it again as {Second}"));
    }

    [Test]
    public void WaitAsync_UnknownAgainAfterTheRestart_Fails()
    {
        _api.Setup(x => x.GetStatusAsync(Target, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Status(First, WorkStatuses.Unknown));

        CliException exception = Assert.ThrowsAsync<CliException>(() => RunAsync(CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), _ => Task.FromResult(Second), CancellationToken.None)))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.Error));
        Assert.That(exception.Message, Does.Contain($"no longer knows schema deployment {Second}").And.Contain("its status is Unknown"));
        Assert.That(exception.Hint, Does.Contain("restarted"));
    }

    [Test]
    public void WaitAsync_UnknownWithoutRestart_FailsWithTheLastProblem()
    {
        _api.Setup(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeployApiException(HttpStatusCode.InternalServerError, "Umbraco Deploy on Live responded 500 Internal Server Error."));

        CliException exception = Assert.ThrowsAsync<CliException>(() => RunAsync(CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), restart: null, CancellationToken.None)))!;

        Assert.That(exception.Message, Does.Contain("responded 500"));
    }

    [Test]
    public void WaitAsync_NonTransientError_IsThrown()
    {
        _api.Setup(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeployApiException(HttpStatusCode.Unauthorized, "rejected"));

        DeployApiException exception = Assert.ThrowsAsync<DeployApiException>(() => CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), restart: null, CancellationToken.None))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.AuthenticationFailed));
    }

    [Test]
    public void WaitAsync_Timeout_ThrowsWithTimeoutExitCode()
    {
        _api.Setup(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>())).ReturnsAsync(Status(First, WorkStatuses.Executing));

        CliException exception = Assert.ThrowsAsync<CliException>(() => RunAsync(CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(1), restart: null, CancellationToken.None)))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.Timeout));
        Assert.That(exception.Message, Does.Contain("1m").And.Contain("Executing").And.Contain("continues on Live"));
        Assert.That(exception.Hint, Does.Contain($"show {First} --wait"));
    }

    [Test]
    public void WaitAsync_Cancelled_WarnsThatTheSchemaDeploymentContinues()
    {
        using var cancellation = new CancellationTokenSource();
        _api.Setup(x => x.GetStatusAsync(Target, First, It.IsAny<CancellationToken>())).ReturnsAsync(Status(First, WorkStatuses.Executing));

        Task<SchemaWaitResult> wait = CreateWaiter().WaitAsync(Target, First, TimeSpan.FromMinutes(15), restart: null, cancellation.Token);
        cancellation.Cancel();

        Assert.That(async () => await wait, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(_stderr.ToString(), Does.Contain("warning: Stopped waiting; the schema deployment continues on Live."));
    }

    private static WorkStatusResponse Status(Guid id, string status, string? comment = null, int percent = -1)
        => new() { Id = id, Status = status, Comment = comment, Percent = percent };

    private Task<T> RunAsync<T>(Task<T> task) => TimeAdvancer.RunAsync(task, _timeProvider, SchemaDeploymentWaiter.PollInterval);

    // Without the timestamps of the plain reporter
    private string[] Lines()
        => _stderr.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.StartsWith('[') ? line[(line.IndexOf(']', StringComparison.Ordinal) + 2)..] : line)
            .ToArray();

    private SchemaDeploymentWaiter CreateWaiter() => new(_api.Object, _timeProvider, new PlainReporter(_stderr, _timeProvider, verbose: false));
}
