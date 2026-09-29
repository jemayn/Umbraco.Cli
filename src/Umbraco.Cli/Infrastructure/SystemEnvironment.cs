namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The real process environment.
/// </summary>
internal sealed class SystemEnvironment : IEnvironment
{
    public string CurrentDirectory => Environment.CurrentDirectory;

    public bool IsInputRedirected => Console.IsInputRedirected;

    public bool IsErrorRedirected => Console.IsErrorRedirected;

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);
}
