using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// A non-interactive environment with controllable variables (redirected stdin and stderr, like CI).
/// </summary>
internal sealed class FakeEnvironment : IEnvironment
{
    public FakeEnvironment(string? currentDirectory = null) => CurrentDirectory = currentDirectory ?? Path.GetTempPath();

    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    public string CurrentDirectory { get; set; }

    public bool IsInputRedirected { get; set; } = true;

    public bool IsErrorRedirected { get; set; } = true;

    public string? GetVariable(string name) => Variables.TryGetValue(name, out string? value) ? value : null;
}
