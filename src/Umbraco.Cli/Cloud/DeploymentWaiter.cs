using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Polls a Deployment until it completes or fails, printing its new status messages.
/// </summary>
internal sealed class DeploymentWaiter
{
    /// <summary>
    /// The interval between status polls, which keeps well within the API limit of 10 calls per minute.
    /// </summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(25);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    private readonly ICloudApiClient _cloudApiClient;
    private readonly TimeProvider _timeProvider;
    private readonly IReporter _reporter;

    public DeploymentWaiter(ICloudApiClient cloudApiClient, TimeProvider timeProvider, IReporter reporter)
    {
        _cloudApiClient = cloudApiClient;
        _timeProvider = timeProvider;
        _reporter = reporter;
    }

    /// <exception cref="CliException">The timeout elapsed before the Deployment completed or failed (exit code 4).</exception>
    public async Task<DeploymentResponse> WaitAsync(CloudCredentials credentials, string deploymentId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await _reporter.TrackWaitAsync($"deployment {deploymentId}", sink => PollAsync(credentials, deploymentId, timeout, sink, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _reporter.Warning(
                "Stopped waiting; the deployment continues on Umbraco Cloud.",
                $"Run 'umbraco cloud deployments show {deploymentId} --wait' to keep waiting.");
            throw;
        }
    }

    private async Task<DeploymentResponse> PollAsync(CloudCredentials credentials, string deploymentId, TimeSpan timeout, IStatusSink sink, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = _timeProvider.GetUtcNow() + timeout;
        var seen = new HashSet<(DateTimeOffset Timestamp, string Message)>();
        DateTimeOffset? lastModified = null;
        string? lastState = null;

        while (true)
        {
            DeploymentResponse deployment = await _cloudApiClient.GetDeploymentAsync(credentials, deploymentId, lastModified, cancellationToken);
            if (deployment.DeploymentState != lastState)
            {
                lastState = deployment.DeploymentState;
                sink.State(deployment.DeploymentState);
            }

            foreach (DeploymentStatusMessage message in deployment.DeploymentStatusMessages.OrderBy(message => message.TimestampUtc))
            {
                if (seen.Add((message.TimestampUtc, message.Message)))
                {
                    sink.Message(message.TimestampUtc, message.Message);
                }

                if (lastModified is null || message.TimestampUtc > lastModified)
                {
                    lastModified = message.TimestampUtc;
                }
            }

            if (deployment.IsTerminal)
            {
                return deployment;
            }

            TimeSpan remaining = deadline - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                throw new CliException(
                    $"Timed out after {Durations.Format(timeout)} waiting for deployment {deploymentId} (state: {deployment.DeploymentState}); the deployment continues on Umbraco Cloud.",
                    ExitCodes.Timeout,
                    $"Run 'umbraco cloud deployments show {deploymentId} --wait' to keep waiting.");
            }

            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, _timeProvider, cancellationToken);
        }
    }
}
