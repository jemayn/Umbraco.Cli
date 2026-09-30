namespace Umbraco.Cli.Infrastructure.Processes;

/// <summary>
/// The .NET SDK operations the CLI needs.
/// </summary>
internal interface IDotnetCli
{
    /// <summary>
    /// Gets the version of the SDK that is selected in <paramref name="directory" /> (respecting <c>global.json</c>).
    /// </summary>
    Task<string> GetSdkVersionAsync(string directory, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the JSON output of <c>dotnet package list --include-transitive --format json</c> for a project (restoring it first).
    /// </summary>
    Task<string> ListPackagesAsync(string projectPath, bool interactive, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <c>dotnet</c> with the given arguments.
    /// </summary>
    /// <exception cref="CliException">The command exits with a non-zero exit code (the message includes its output).</exception>
    Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken);
}
