namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Writes progress and diagnostics to stderr, keeping stdout free for command results.
/// </summary>
internal interface IReporter
{
    void Info(string message);

    void Warning(string message, string? hint = null);

    void Error(string message, string? hint = null);

    void Verbose(string message);

    /// <summary>
    /// Shows byte-level progress while <paramref name="action" /> uploads <paramref name="totalBytes" /> bytes.
    /// </summary>
    Task<T> TrackUploadAsync<T>(string description, long totalBytes, Func<IProgress<long>, Task<T>> action);

    /// <summary>
    /// Shows that the CLI is waiting while <paramref name="action" /> runs, printing the states and messages it reports.
    /// </summary>
    Task<T> TrackWaitAsync<T>(string description, Func<IStatusSink, Task<T>> action);
}
