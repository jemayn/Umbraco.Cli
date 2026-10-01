using Umbraco.Cli.Deploy.Schema.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// <c>umbraco deploy schema deploy</c>: starts a schema deployment on an Environment and waits for it.
/// </summary>
internal sealed class SchemaDeployCommandHandler
{
    private readonly IEnvironment _environment;
    private readonly IDeployApiClient _deployApiClient;
    private readonly SchemaDeploymentWaiter _waiter;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public SchemaDeployCommandHandler(IEnvironment environment, IDeployApiClient deployApiClient, SchemaDeploymentWaiter waiter, IOutput output, IReporter reporter)
    {
        _environment = environment;
        _deployApiClient = deployApiClient;
        _waiter = waiter;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(SchemaDeploySettings settings, CancellationToken cancellationToken)
    {
        SchemaTarget target = SchemaTargetResolver.Resolve(settings.Target, _environment);
        _reporter.Verbose($"Using the credentials from {target.Credentials.Source} for {target.Url}.");

        Guid taskId = await StartAsync(target, settings.Reason, cancellationToken);
        if (settings.NoWait)
        {
            return SchemaDeploymentResultWriter.Write(SchemaDeploymentResult.Started(target, taskId), target.DisplayName, _output, _reporter);
        }

        SchemaWaitResult result = await _waiter.WaitAsync(target, taskId, settings.Timeout, token => StartAsync(target, settings.Reason, token), cancellationToken);
        return SchemaDeploymentResultWriter.Write(SchemaDeploymentResult.From(target, result.TaskId, result.Status, result.Restarted), target.DisplayName, _output, _reporter);
    }

    private async Task<Guid> StartAsync(SchemaTarget target, string reason, CancellationToken cancellationToken)
    {
        ExtractStartResponse response = await _deployApiClient.StartAsync(target, reason, cancellationToken);
        if (response.TaskId == Guid.Empty)
        {
            throw new CliException($"Umbraco Deploy on {target.DisplayName} didn't start the schema deployment.", ExitCodes.Error, "Check the Umbraco Deploy log of the Environment.");
        }

        // Since Umbraco Deploy 17.4, a start joins the schema deployment that is already queued or running (or that completed on startup)
        _reporter.Info(response.TaskStarted
            ? $"Schema deployment {response.TaskId} to {target.DisplayName} started."
            : $"Joined schema deployment {response.TaskId} on {target.DisplayName}, which was already queued, running or completed.");
        return response.TaskId;
    }
}
