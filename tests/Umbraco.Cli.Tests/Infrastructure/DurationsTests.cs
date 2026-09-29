using NUnit.Framework;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class DurationsTests
{
    [TestCase("90s", 90)]
    [TestCase("15m", 900)]
    [TestCase("1h30m", 5400)]
    [TestCase("1H", 3600)]
    [TestCase("00:15:00", 900)]
    public void TryParse_ValidDuration_ReturnsSeconds(string value, int expectedSeconds)
    {
        bool parsed = Durations.TryParse(value, out TimeSpan duration);

        Assert.That(parsed, Is.True);
        Assert.That(duration, Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
    }

    [TestCase("")]
    [TestCase("soon")]
    [TestCase("0s")]
    [TestCase("-5m")]
    public void TryParse_InvalidDuration_ReturnsFalse(string value)
        => Assert.That(Durations.TryParse(value, out _), Is.False);

    [TestCase(90, "1m 30s")]
    [TestCase(900, "15m")]
    [TestCase(5400, "1h 30m")]
    [TestCase(0, "0s")]
    public void Format_ReturnsCompactText(int seconds, string expected)
        => Assert.That(Durations.Format(TimeSpan.FromSeconds(seconds)), Is.EqualTo(expected));
}
