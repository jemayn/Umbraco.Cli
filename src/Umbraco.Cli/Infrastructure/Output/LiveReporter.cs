using System.Diagnostics;
using System.Globalization;
using Spectre.Console;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Renders live progress (progress bars and spinners) to an interactive stderr using Spectre.Console.
/// </summary>
internal sealed class LiveReporter : IReporter
{
    private readonly IAnsiConsole _console;
    private readonly TaskbarProgress _taskbar;
    private readonly TimeProvider _timeProvider;
    private readonly bool _verbose;

    public LiveReporter(IAnsiConsole console, TaskbarProgress taskbar, TimeProvider timeProvider, bool verbose)
    {
        _console = console;
        _taskbar = taskbar;
        _timeProvider = timeProvider;
        _verbose = verbose;
    }

    public void Info(string message) => _console.MarkupLine(Markup.Escape(message));

    public void Warning(string message, string? hint = null)
    {
        _console.MarkupLine($"[yellow]warning:[/] {Markup.Escape(message)}");
        WriteHint(hint);
    }

    public void Error(string message, string? hint = null)
    {
        _console.MarkupLine($"[red]error:[/] {Markup.Escape(message)}");
        WriteHint(hint);
    }

    public void Verbose(string message)
    {
        if (_verbose)
        {
            _console.MarkupLine($"[grey]verbose: {Markup.Escape(message)}[/]");
        }
    }

    public async Task<T> TrackUploadAsync<T>(string description, long totalBytes, Func<IProgress<long>, Task<T>> action)
    {
        try
        {
            return await _console.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new DownloadedColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async context =>
                {
                    ProgressTask task = context.AddTask(Markup.Escape(description), maxValue: Math.Max(totalBytes, 1));
                    var progress = new SynchronousProgress(totalBytes, sent =>
                    {
                        task.Value = sent;
                        _taskbar.SetPercentage(totalBytes <= 0 ? 100 : (int)(sent * 100 / totalBytes));
                        if (sent >= totalBytes)
                        {
                            task.Description = $"{Markup.Escape(description)} (awaiting response)";
                        }
                    });

                    T result = await action(progress);
                    task.Value = task.MaxValue;
                    task.StopTask();
                    return result;
                });
        }
        finally
        {
            _taskbar.Clear();
        }
    }

    public async Task<T> TrackWaitAsync<T>(string description, Func<IStatusSink, Task<T>> action)
    {
        long started = _timeProvider.GetTimestamp();
        string state = "Starting";
        string Status() => string.Create(
            CultureInfo.InvariantCulture,
            $"{Markup.Escape(description)}: {Markup.Escape(state)} ({_timeProvider.GetElapsedTime(started):mm\\:ss})");

        _taskbar.SetIndeterminate();
        try
        {
            return await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(Status(), async context =>
                {
                    using var refresh = new CancellationTokenSource();
                    Task ticker = TickAsync(context, Status, refresh.Token);
                    try
                    {
                        return await action(new LiveStatusSink(
                            _console,
                            _timeProvider,
                            newState =>
                            {
                                state = newState;
                                context.Status(Status());
                            }));
                    }
                    finally
                    {
                        await refresh.CancelAsync();
                        await ticker;
                    }
                });
        }
        finally
        {
            _taskbar.Clear();
        }
    }

    private static async Task TickAsync(StatusContext context, Func<string> status, CancellationToken cancellationToken)
    {
        try
        {
            while (cancellationToken.IsCancellationRequested is false)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                context.Status(status());
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped because the wait completed
        }
    }

    private void WriteHint(string? hint)
    {
        if (string.IsNullOrEmpty(hint) is false)
        {
            _console.MarkupLine($"[grey]hint:[/] {Markup.Escape(hint)}");
        }
    }

    private sealed class SynchronousProgress : IProgress<long>
    {
        private readonly long _total;
        private readonly Action<long> _report;
        private readonly Stopwatch _throttle = Stopwatch.StartNew();

        public SynchronousProgress(long total, Action<long> report)
        {
            _total = total;
            _report = report;
        }

        public void Report(long value)
        {
            // Throttle to about 10 updates per second, but always report completion
            if (_throttle.ElapsedMilliseconds >= 100 || value >= _total)
            {
                _throttle.Restart();
                _report(value);
            }
        }
    }

    private sealed class LiveStatusSink : IStatusSink
    {
        private readonly IAnsiConsole _console;
        private readonly TimeProvider _timeProvider;
        private readonly Action<string> _onState;

        public LiveStatusSink(IAnsiConsole console, TimeProvider timeProvider, Action<string> onState)
        {
            _console = console;
            _timeProvider = timeProvider;
            _onState = onState;
        }

        public void State(string state) => _onState(state);

        public void Message(DateTimeOffset timestamp, string message)
        {
            DateTimeOffset local = TimeZoneInfo.ConvertTime(timestamp, _timeProvider.LocalTimeZone);
            _console.MarkupLine($"[grey]{local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}[/] {Markup.Escape(message)}");
        }
    }
}
