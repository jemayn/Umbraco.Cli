using NUnit.Framework;
using Spectre.Console;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class LiveReporterTests
{
    private StringWriter _output = null!;
    private StringWriter _taskbarOutput = null!;
    private LiveReporter _reporter = null!;

    [SetUp]
    public void SetUp()
    {
        _output = new StringWriter();
        _taskbarOutput = new StringWriter();
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(_output),
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.Standard,
            Interactive = InteractionSupport.Yes,

            // Spectre's default enrichers detect CI (such as GitHub Actions) and turn off interactive rendering
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        });
        _reporter = new LiveReporter(console, new TaskbarProgress(_taskbarOutput, enabled: true), TimeProvider.System, verbose: true);
    }

    [TearDown]
    public void TearDown()
    {
        _output.Dispose();
        _taskbarOutput.Dispose();
    }

    [Test]
    public async Task TrackUploadAsync_RendersProgressAndSetsTaskbarProgress()
    {
        int result = await _reporter.TrackUploadAsync("Uploading [site].zip", 1000, progress =>
        {
            progress.Report(0);
            progress.Report(500);
            progress.Report(1000);
            return Task.FromResult(42);
        });

        Assert.That(result, Is.EqualTo(42));
        Assert.That(_output.ToString(), Does.Contain("Uploading [site].zip"));
        Assert.That(_taskbarOutput.ToString(), Does.Contain("\u001b]9;4;1;100\u0007").And.EndWith("\u001b]9;4;0;0\u0007"));
    }

    [Test]
    public async Task TrackWaitAsync_PrintsMessagesAboveTheSpinner()
    {
        await _reporter.TrackWaitAsync("deployment d-1", sink =>
        {
            sink.State("InProgress");
            sink.Message(DateTimeOffset.UtcNow, "Restoring [packages]");
            return Task.FromResult(0);
        });

        Assert.That(_output.ToString(), Does.Contain("Restoring [packages]"));
        Assert.That(_taskbarOutput.ToString(), Does.StartWith("\u001b]9;4;3;0\u0007").And.EndWith("\u001b]9;4;0;0\u0007"));
    }

    [Test]
    public void Diagnostics_EscapeMarkup()
    {
        _reporter.Warning("Use [brackets]", "hint [x]");
        _reporter.Error("Failed [y]");
        _reporter.Verbose("Detail [z]");

        Assert.That(_output.ToString(), Does.Contain("[brackets]").And.Contain("hint [x]").And.Contain("Failed [y]").And.Contain("Detail [z]"));
    }
}
