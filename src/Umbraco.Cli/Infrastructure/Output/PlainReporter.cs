using System.Globalization;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Writes progress as plain, timestamped lines (used when stderr is redirected, in CI, or with <c>--no-progress</c>).
/// </summary>
internal sealed class PlainReporter : IReporter
{
    private readonly TextWriter _stderr;
    private readonly TimeProvider _timeProvider;
    private readonly bool _verbose;

    public PlainReporter(TextWriter stderr, TimeProvider timeProvider, bool verbose)
    {
        _stderr = stderr;
        _timeProvider = timeProvider;
        _verbose = verbose;
    }

    public void Info(string message) => WriteLine(message);

    public void Warning(string message, string? hint = null)
    {
        WriteLine($"warning: {message}");
        WriteHint(hint);
    }

    public void Error(string message, string? hint = null)
    {
        WriteLine($"error: {message}");
        WriteHint(hint);
    }

    public void Verbose(string message)
    {
        if (_verbose)
        {
            WriteLine($"verbose: {message}");
        }
    }

    public async Task<T> TrackUploadAsync<T>(string description, long totalBytes, Func<IProgress<long>, Task<T>> action)
    {
        WriteTimestamped(_timeProvider.GetUtcNow(), $"{description}: uploading {ByteSize.Format(totalBytes)}");
        var progress = new PercentageProgress(totalBytes, percentage =>
        {
            WriteTimestamped(_timeProvider.GetUtcNow(), percentage < 100
                ? $"{description}: {percentage}%"
                : $"{description}: 100%, awaiting response");
        });

        return await action(progress);
    }

    public async Task<T> TrackWaitAsync<T>(string description, Func<IStatusSink, Task<T>> action)
    {
        WriteTimestamped(_timeProvider.GetUtcNow(), $"Waiting for {description}");
        return await action(new PlainStatusSink(this));
    }

    private void WriteTimestamped(DateTimeOffset timestamp, string message)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(timestamp, _timeProvider.LocalTimeZone);
        WriteLine($"[{local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] {message}");
    }

    private void WriteHint(string? hint)
    {
        if (string.IsNullOrEmpty(hint) is false)
        {
            WriteLine($"hint: {hint}");
        }
    }

    private void WriteLine(string line)
    {
        _stderr.WriteLine(line);
        _stderr.Flush();
    }

    private sealed class PlainStatusSink : IStatusSink
    {
        private readonly PlainReporter _reporter;

        public PlainStatusSink(PlainReporter reporter) => _reporter = reporter;

        public void State(string state) => _reporter.WriteTimestamped(_reporter._timeProvider.GetUtcNow(), $"State: {state}");

        public void Message(DateTimeOffset timestamp, string message) => _reporter.WriteTimestamped(timestamp, message);
    }
}
