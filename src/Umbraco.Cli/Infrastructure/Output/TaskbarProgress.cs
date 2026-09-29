using System.Globalization;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Emits OSC 9;4 sequences that show progress in the taskbar of terminals that support it (Windows Terminal, ConEmu).
/// </summary>
/// <remarks>
/// Other terminals may interpret OSC 9 as a desktop notification, so nothing is written unless support is detected.
/// </remarks>
internal sealed class TaskbarProgress
{
    private readonly TextWriter _writer;
    private readonly bool _enabled;

    public TaskbarProgress(TextWriter writer, bool enabled)
    {
        _writer = writer;
        _enabled = enabled;
    }

    public void SetPercentage(int percentage) => Write(1, Math.Clamp(percentage, 0, 100));

    public void SetIndeterminate() => Write(3, 0);

    public void Clear() => Write(0, 0);

    private void Write(int state, int percentage)
    {
        if (_enabled)
        {
            _writer.Write(string.Create(CultureInfo.InvariantCulture, $"\u001b]9;4;{state};{percentage}\u0007"));
            _writer.Flush();
        }
    }
}
