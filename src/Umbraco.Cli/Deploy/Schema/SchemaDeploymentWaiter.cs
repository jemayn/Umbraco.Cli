using Umbraco.Cli.Deploy.Schema.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// Polls a schema deployment until it completes or fails, printing its status and progress.
/// </summary>
/// <remarks>
/// Umbraco Deploy only keeps the status in memory: right after the start (before the task runs) and after the site restarted, the status is
/// <c>Unknown</c> or the request fails. Both are tolerated for <see cref="UnknownGracePeriod" />; after that, the schema deployment is started
/// once more when a restart is given (as <c>TriggerDeploy.ps1</c> does), and otherwise it fails.
/// </remarks>
internal sealed class SchemaDeploymentWaiter
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan UnknownGracePeriod = TimeSpan.FromSeconds(60);

    private readonly IDeployApiClient _deployApiClient;
    private readonly TimeProvider _timeProvider;
    private readonly IReporter _reporter;

    public SchemaDeploymentWaiter(IDeployApiClient deployApiClient, TimeProvider timeProvider, IReporter reporter)
    {
        _deployApiClient = deployApiClient;
        _timeProvider = timeProvider;
        _reporter = reporter;
    }

    /// <param name="target">The Environment.</param>
    /// <param name="taskId">The task ID of the schema deployment.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="restart">Starts the schema deployment again (once) when its status stays unknown, returning the new task ID; <c>null</c> to fail instead.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="CliException">The timeout elapsed (exit code 4), or the status stayed unknown (exit code 1).</exception>
    public async Task<SchemaWaitResult> WaitAsync(SchemaTarget target, Guid taskId, TimeSpan timeout, Func<CancellationToken, Task<Guid>>? restart, CancellationToken cancellationToken)
    {
        var state = new WaitState(taskId);
        try
        {
            return await _reporter.TrackWaitAsync($"schema deployment {taskId}", sink => PollAsync(target, state, timeout, restart, sink, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _reporter.Warning($"Stopped waiting; the schema deployment continues on {target.DisplayName}.", ShowHint(state.TaskId));
            throw;
        }
    }

    private static string ShowHint(Guid taskId) => $"Run 'umbraco deploy schema show {taskId} --wait' with the same target options to keep waiting.";

    private async Task<SchemaWaitResult> PollAsync(SchemaTarget target, WaitState state, TimeSpan timeout, Func<CancellationToken, Task<Guid>>? restart, IStatusSink sink, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = _timeProvider.GetUtcNow() + timeout;
        DateTimeOffset? unknownSince = _timeProvider.GetUtcNow();
        string? lastStatus = null;
        string? lastProgress = null;
        string? lastProblem = null;

        while (true)
        {
            WorkStatusResponse? status = null;
            try
            {
                status = await _deployApiClient.GetStatusAsync(target, state.TaskId, cancellationToken);
            }
            catch (DeployApiException exception) when (exception.IsTransient)
            {
                lastProblem = exception.Message;
                _reporter.Verbose($"Getting the status of schema deployment {state.TaskId} failed: {exception.Message}");
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (status is { IsKnown: true })
            {
                unknownSince = null;
                if (status.Status != lastStatus)
                {
                    lastStatus = status.Status;
                    sink.State(status.Status);
                }

                string? progress = FormatProgress(status);
                if (progress is not null && progress != lastProgress)
                {
                    lastProgress = progress;
                    sink.Message(now, progress);
                }

                if (status.IsTerminal)
                {
                    return new SchemaWaitResult(state.TaskId, status, state.Restarted);
                }
            }
            else
            {
                lastProblem = status is null ? lastProblem : $"its status is {status.Status}";
                unknownSince ??= now;
                if (now - unknownSince >= UnknownGracePeriod)
                {
                    if (restart is null || state.Restarted)
                    {
                        throw new CliException(
                            $"Umbraco Deploy on {target.DisplayName} no longer knows schema deployment {state.TaskId} ({lastProblem}).",
                            ExitCodes.Error,
                            "The site may have restarted, or another Umbraco Deploy operation was running; check the Umbraco Deploy log of the Environment.");
                    }

                    Guid previous = state.TaskId;
                    state.TaskId = await restart(cancellationToken);
                    state.Restarted = true;
                    unknownSince = _timeProvider.GetUtcNow();
                    lastStatus = null;
                    lastProgress = null;
                    sink.Message(unknownSince.Value, $"Schema deployment {previous} is no longer known (the site may have restarted or was busy); started it again as {state.TaskId}.");
                }
            }

            TimeSpan remaining = deadline - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                throw new CliException(
                    $"Timed out after {Durations.Format(timeout)} waiting for schema deployment {state.TaskId} (status: {lastStatus ?? WorkStatuses.Unknown}); the schema deployment continues on {target.DisplayName}.",
                    ExitCodes.Timeout,
                    ShowHint(state.TaskId));
            }

            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, _timeProvider, cancellationToken);
        }
    }

    private static string? FormatProgress(WorkStatusResponse status)
    {
        if (string.IsNullOrWhiteSpace(status.Comment))
        {
            return null;
        }

        return status.Percent >= 0 ? $"{status.Comment.Trim()} ({status.Percent}%)" : status.Comment.Trim();
    }

    /// <summary>
    /// The task being waited on, which changes when the schema deployment is started again.
    /// </summary>
    private sealed class WaitState(Guid taskId)
    {
        public Guid TaskId { get; set; } = taskId;

        public bool Restarted { get; set; }
    }
}

/// <summary>
/// The final status of a schema deployment, and whether it was started again while waiting.
/// </summary>
internal sealed record SchemaWaitResult(Guid TaskId, WorkStatusResponse Status, bool Restarted);
