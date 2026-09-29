using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Writes command results to stdout: one JSON object in JSON mode, otherwise the formatted text.
/// </summary>
internal sealed class ConsoleOutput : IOutput
{
    private readonly GlobalSettings _settings;
    private readonly TextWriter _stdout;

    public ConsoleOutput(GlobalSettings settings, TextWriter stdout)
    {
        _settings = settings;
        _stdout = stdout;
    }

    public void Write<T>(T result, JsonTypeInfo<T> jsonTypeInfo, Func<T, string> formatText)
    {
        _stdout.WriteLine(_settings.Output == OutputFormat.Json ? JsonSerializer.Serialize(result, jsonTypeInfo) : formatText(result));
        _stdout.Flush();
    }
}
