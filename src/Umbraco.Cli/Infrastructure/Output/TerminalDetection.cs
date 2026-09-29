namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Decides how progress is rendered.
/// </summary>
internal static class TerminalDetection
{
    private static readonly string[] CiVariables = ["CI", "TF_BUILD", "GITHUB_ACTIONS"];

    /// <summary>
    /// Live progress is only used on an interactive stderr outside CI, unless disabled with <c>--no-progress</c>.
    /// </summary>
    public static bool UseLiveProgress(GlobalSettings settings, IEnvironment environment)
        => settings.NoProgress is false &&
           environment.IsErrorRedirected is false &&
           CiVariables.All(name => string.IsNullOrEmpty(environment.GetVariable(name)));

    /// <summary>
    /// Taskbar progress is only emitted in terminals known to support OSC 9;4.
    /// </summary>
    public static bool SupportsTaskbarProgress(IEnvironment environment)
        => string.IsNullOrEmpty(environment.GetVariable("WT_SESSION")) is false ||
           string.Equals(environment.GetVariable("ConEmuANSI"), "ON", StringComparison.OrdinalIgnoreCase);
}
