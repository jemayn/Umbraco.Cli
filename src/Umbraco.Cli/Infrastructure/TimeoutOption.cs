using System.CommandLine;
using System.CommandLine.Parsing;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Creates the <c>--timeout</c> option of the commands that wait, accepting durations such as <c>90s</c>, <c>15m</c> or <c>1h30m</c>.
/// </summary>
internal static class TimeoutOption
{
    public static Option<TimeSpan> Create(string description, TimeSpan defaultTimeout) => new("--timeout")
    {
        Description = description,
        DefaultValueFactory = _ => defaultTimeout,
        CustomParser = result => Parse(result, defaultTimeout),
        HelpName = "duration",
    };

    private static TimeSpan Parse(ArgumentResult result, TimeSpan defaultTimeout)
    {
        if (result.Tokens.Count == 0)
        {
            return defaultTimeout;
        }

        string value = result.Tokens[0].Value;
        if (Durations.TryParse(value, out TimeSpan duration))
        {
            return duration;
        }

        result.AddError($"'{value}' is not a valid duration; use for example 90s, 15m, 1h30m or 00:15:00.");
        return default;
    }
}
