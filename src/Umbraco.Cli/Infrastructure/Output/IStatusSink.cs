namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Receives the state changes and status messages of an operation that is being waited on.
/// </summary>
internal interface IStatusSink
{
    void State(string state);

    void Message(DateTimeOffset timestamp, string message);
}
