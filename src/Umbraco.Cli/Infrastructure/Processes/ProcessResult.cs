namespace Umbraco.Cli.Infrastructure.Processes;

/// <summary>
/// The outcome of running an external process.
/// </summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
