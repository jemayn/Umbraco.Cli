namespace Umbraco.Cli.Infrastructure.Processes;

/// <summary>
/// Runs external processes (git, dotnet) and captures their output.
/// </summary>
internal interface IProcessRunner
{
    /// <exception cref="CliException">The executable could not be started (for example, because it is not installed).</exception>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken);
}
