namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The real process environment.
/// </summary>
internal sealed class SystemEnvironment : IEnvironment
{
    public string CurrentDirectory => Environment.CurrentDirectory;

    public string CacheDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Umbraco.Cli")
        : Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } cache ? cache : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"),
            "umbraco-cli");

    public bool IsInputRedirected => Console.IsInputRedirected;

    public bool IsErrorRedirected => Console.IsErrorRedirected;

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);
}
