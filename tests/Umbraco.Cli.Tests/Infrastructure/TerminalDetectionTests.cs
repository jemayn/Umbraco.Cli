using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class TerminalDetectionTests
{
    private static readonly GlobalSettings Defaults = new(OutputFormat.Text, NoProgress: false, Verbose: false);

    [Test]
    public void UseLiveProgress_InteractiveStderrOutsideCi_ReturnsTrue()
        => Assert.That(TerminalDetection.UseLiveProgress(Defaults, new FakeEnvironment { IsErrorRedirected = false }), Is.True);

    [Test]
    public void UseLiveProgress_RedirectedStderr_ReturnsFalse()
        => Assert.That(TerminalDetection.UseLiveProgress(Defaults, new FakeEnvironment { IsErrorRedirected = true }), Is.False);

    [Test]
    public void UseLiveProgress_NoProgressOption_ReturnsFalse()
        => Assert.That(TerminalDetection.UseLiveProgress(Defaults with { NoProgress = true }, new FakeEnvironment { IsErrorRedirected = false }), Is.False);

    [TestCase("CI")]
    [TestCase("TF_BUILD")]
    [TestCase("GITHUB_ACTIONS")]
    public void UseLiveProgress_InCi_ReturnsFalse(string variable)
    {
        var environment = new FakeEnvironment { IsErrorRedirected = false };
        environment.Variables[variable] = "true";

        Assert.That(TerminalDetection.UseLiveProgress(Defaults, environment), Is.False);
    }

    [Test]
    public void SupportsTaskbarProgress_OnlyInWindowsTerminalOrConEmu()
    {
        var plain = new FakeEnvironment();
        var windowsTerminal = new FakeEnvironment();
        windowsTerminal.Variables["WT_SESSION"] = "abc";
        var conEmu = new FakeEnvironment();
        conEmu.Variables["ConEmuANSI"] = "ON";

        Assert.That(TerminalDetection.SupportsTaskbarProgress(plain), Is.False);
        Assert.That(TerminalDetection.SupportsTaskbarProgress(windowsTerminal), Is.True);
        Assert.That(TerminalDetection.SupportsTaskbarProgress(conEmu), Is.True);
    }
}
