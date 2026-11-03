namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// An environment whose variables fall back to the <see cref="LocalEnvironmentFile" /> of the current repository, so the values pulled
/// from Umbraco Cloud are used without being exported. Variables set in the process environment take precedence (such as in CI/CD pipelines).
/// </summary>
internal sealed class LocalFileEnvironment : IEnvironment
{
    private readonly IEnvironment _inner;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _fileValues;

    public LocalFileEnvironment(IEnvironment inner)
    {
        _inner = inner;
        _fileValues = new Lazy<IReadOnlyDictionary<string, string>>(() =>
            LocalEnvironmentFile.Find(inner.CurrentDirectory) is string path
                ? LocalEnvironmentFile.Read(path)
                : new Dictionary<string, string>());
    }

    public string CurrentDirectory => _inner.CurrentDirectory;

    public string CacheDirectory => _inner.CacheDirectory;

    public bool IsInputRedirected => _inner.IsInputRedirected;

    public bool IsErrorRedirected => _inner.IsErrorRedirected;

    public string? GetVariable(string name)
        => _inner.GetVariable(name) is { Length: > 0 } value
            ? value
            : _fileValues.Value.TryGetValue(name, out string? fileValue) ? fileValue : null;
}
