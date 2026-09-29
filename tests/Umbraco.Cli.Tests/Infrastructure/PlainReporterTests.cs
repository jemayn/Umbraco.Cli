using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class PlainReporterTests
{
    private StringWriter _stderr = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _stderr = new StringWriter { NewLine = "\n" };
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 14, 2, 11, TimeSpan.Zero));
        _timeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);
    }

    [TearDown]
    public void TearDown() => _stderr.Dispose();

    [Test]
    public void Warning_WritesPrefixAndHint()
    {
        var reporter = new PlainReporter(_stderr, _timeProvider, verbose: false);

        reporter.Warning("Careful.", "Do this instead.");

        Assert.That(_stderr.ToString(), Is.EqualTo("warning: Careful.\nhint: Do this instead.\n"));
    }

    [Test]
    public void Verbose_IsOnlyWrittenWhenEnabled()
    {
        new PlainReporter(_stderr, _timeProvider, verbose: false).Verbose("hidden");
        new PlainReporter(_stderr, _timeProvider, verbose: true).Verbose("shown");

        Assert.That(_stderr.ToString(), Is.EqualTo("verbose: shown\n"));
    }

    [Test]
    public async Task TrackUploadAsync_WritesEveryTenPercentOnce()
    {
        var reporter = new PlainReporter(_stderr, _timeProvider, verbose: false);

        string result = await reporter.TrackUploadAsync("Uploading site.zip", 100, progress =>
        {
            foreach (long sent in new long[] { 0, 5, 10, 12, 55, 100 })
            {
                progress.Report(sent);
            }

            return Task.FromResult("done");
        });

        Assert.That(result, Is.EqualTo("done"));
        Assert.That(_stderr.ToString(), Is.EqualTo(
            "[14:02:11] Uploading site.zip: uploading 100 B\n" +
            "[14:02:11] Uploading site.zip: 10%\n" +
            "[14:02:11] Uploading site.zip: 50%\n" +
            "[14:02:11] Uploading site.zip: 100%, awaiting response\n"));
    }

    [Test]
    public async Task TrackWaitAsync_WritesStatesAndMessagesWithTheirTimestamps()
    {
        var reporter = new PlainReporter(_stderr, _timeProvider, verbose: false);

        await reporter.TrackWaitAsync("deployment 42", sink =>
        {
            sink.State("Queued");
            sink.Message(new DateTimeOffset(2026, 9, 30, 14, 3, 0, TimeSpan.Zero), "Restoring packages");
            return Task.FromResult(0);
        });

        Assert.That(_stderr.ToString(), Is.EqualTo(
            "[14:02:11] Waiting for deployment 42\n" +
            "[14:02:11] State: Queued\n" +
            "[14:03:00] Restoring packages\n"));
    }
}
