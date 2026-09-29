using System.Globalization;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Formats byte counts for humans.
/// </summary>
internal static class ByteSize
{
    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes} B")
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {units[unit]}");
    }
}
