namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The process environment, abstracted so commands can be tested without touching the real console or environment variables.
/// </summary>
internal interface IEnvironment
{
    string CurrentDirectory { get; }

    bool IsInputRedirected { get; }

    bool IsErrorRedirected { get; }

    string? GetVariable(string name);
}
