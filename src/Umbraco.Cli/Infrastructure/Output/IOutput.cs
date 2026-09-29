using System.Text.Json.Serialization.Metadata;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Writes the result of a command to stdout, as text or JSON depending on <c>--output</c>.
/// </summary>
internal interface IOutput
{
    void Write<T>(T result, JsonTypeInfo<T> jsonTypeInfo, Func<T, string> formatText);
}
