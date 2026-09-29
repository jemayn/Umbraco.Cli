using System.Globalization;
using System.Text.RegularExpressions;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Parses and formats durations such as <c>90s</c>, <c>15m</c>, <c>1h30m</c> or <c>00:15:00</c>.
/// </summary>
internal static partial class Durations
{
    public static bool TryParse(string value, out TimeSpan duration)
    {
        Match match = UnitsPattern().Match(value.Trim());
        if (match.Success && match.Length > 0)
        {
            duration = TimeSpan.FromHours(Number(match.Groups["h"])) + TimeSpan.FromMinutes(Number(match.Groups["m"])) + TimeSpan.FromSeconds(Number(match.Groups["s"]));
            return duration > TimeSpan.Zero;
        }

        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out duration) && duration > TimeSpan.Zero;
    }

    public static string Format(TimeSpan duration)
    {
        var parts = new List<string>();
        if (duration.TotalHours >= 1)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours}h"));
        }

        if (duration.Minutes > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{duration.Minutes}m"));
        }

        if (duration.Seconds > 0 || parts.Count == 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{duration.Seconds}s"));
        }

        return string.Join(' ', parts);
    }

    private static int Number(Group group) => group.Success ? int.Parse(group.Value, CultureInfo.InvariantCulture) : 0;

    [GeneratedRegex(@"^(?:(?<h>\d+)h)?(?:(?<m>\d+)m)?(?:(?<s>\d+)s)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnitsPattern();
}
