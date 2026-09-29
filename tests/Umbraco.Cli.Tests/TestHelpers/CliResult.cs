namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// The outcome of running the CLI in-process.
/// </summary>
internal sealed record CliResult(int ExitCode, string Stdout, string Stderr)
{
    public override string ToString() => $"Exit code {ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{Stdout}{Environment.NewLine}stderr:{Environment.NewLine}{Stderr}";
}
