using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class DeploymentWaiterTests
{
    private static readonly CloudCredentials Credentials = new("p", "k");
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _timeProvider = null!;
    private Mock<ICloudApiClient> _api = null!;
    private StringWriter _stderr = null!;

    [SetUp]
    public void SetUp()
    {
        _timeProvider = new FakeTimeProvider(Start);
        _timeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);
        _api = new Mock<ICloudApiClient>(MockBehavior.Strict);
        _stderr = new StringWriter { NewLine = "\n" };
    }

    [TearDown]
    public void TearDown() => _stderr.Dispose();

    [Test]
    public async Task WaitAsync_PollsUntilCompletedAndPrintsEachMessageOnce()
    {
        DateTimeOffset first = Start.AddSeconds(5);
        DateTimeOffset second = Start.AddSeconds(30);
        _api.SetupSequence(x => x.GetDeploymentAsync(Credentials, "d-1", It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Deployment("Queued", (first, "Queued")))
            .ReturnsAsync(Deployment("InProgress", (first, "Queued"), (second, "Building")))
            .ReturnsAsync(Deployment("Completed", (second, "Building")));

        DeploymentResponse result = await TestHelpers.TimeAdvancer.RunAsync(CreateWaiter().WaitAsync(Credentials, "d-1", TimeSpan.FromMinutes(15), CancellationToken.None), _timeProvider, DeploymentWaiter.PollInterval);

        Assert.That(result.IsCompleted, Is.True);

        // State lines are stamped with the (advancing) current time, messages with their own timestamps
        string[] lines = _stderr.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Contains("] State: ", StringComparison.Ordinal) || line.Contains("] Waiting", StringComparison.Ordinal) ? line[(line.IndexOf(']', StringComparison.Ordinal) + 2)..] : line)
            .ToArray();
        Assert.That(lines, Is.EqualTo(new[]
        {
            "Waiting for deployment d-1",
            "State: Queued",
            "[12:00:05] Queued",
            "State: InProgress",
            "[12:00:30] Building",
            "State: Completed",
        }));
        _api.Verify(x => x.GetDeploymentAsync(Credentials, "d-1", null, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(x => x.GetDeploymentAsync(Credentials, "d-1", first, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(x => x.GetDeploymentAsync(Credentials, "d-1", second, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task WaitAsync_ReturnsFailedDeployment()
    {
        _api.Setup(x => x.GetDeploymentAsync(Credentials, "d-1", It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Deployment("Failed", (Start, "Build failed")));

        DeploymentResponse result = await CreateWaiter().WaitAsync(Credentials, "d-1", TimeSpan.FromMinutes(15), CancellationToken.None);

        Assert.That(result.IsFailed, Is.True);
    }

    [Test]
    public void WaitAsync_Timeout_ThrowsWithTimeoutExitCode()
    {
        _api.Setup(x => x.GetDeploymentAsync(Credentials, "d-1", It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Deployment("InProgress"));

        CliException exception = Assert.ThrowsAsync<CliException>(() => TestHelpers.TimeAdvancer.RunAsync(
            CreateWaiter().WaitAsync(Credentials, "d-1", TimeSpan.FromMinutes(1), CancellationToken.None),
            _timeProvider,
            TimeSpan.FromSeconds(5)))!;

        Assert.That(exception.ExitCode, Is.EqualTo(ExitCodes.Timeout));
        Assert.That(exception.Message, Does.Contain("1m").And.Contain("InProgress"));
        Assert.That(exception.Hint, Does.Contain("show d-1 --wait"));
    }

    [Test]
    public void WaitAsync_Cancelled_WarnsThatTheDeploymentContinues()
    {
        using var cancellation = new CancellationTokenSource();
        _api.Setup(x => x.GetDeploymentAsync(Credentials, "d-1", It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Deployment("InProgress"));

        Task<DeploymentResponse> wait = CreateWaiter().WaitAsync(Credentials, "d-1", TimeSpan.FromMinutes(15), cancellation.Token);
        cancellation.Cancel();

        Assert.That(async () => await wait, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(_stderr.ToString(), Does.Contain("warning: Stopped waiting; the deployment continues on Umbraco Cloud."));
    }

    private static DeploymentResponse Deployment(string state, params (DateTimeOffset Timestamp, string Message)[] messages)
        => new()
        {
            DeploymentId = "d-1",
            DeploymentState = state,
            DeploymentStatusMessages = messages.Select(message => new DeploymentStatusMessage { TimestampUtc = message.Timestamp, Message = message.Message }).ToList(),
        };

    private DeploymentWaiter CreateWaiter() => new(_api.Object, _timeProvider, new PlainReporter(_stderr, _timeProvider, verbose: false));
}
