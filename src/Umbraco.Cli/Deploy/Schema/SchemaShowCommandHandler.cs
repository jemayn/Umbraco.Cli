using Umbraco.Cli.Deploy.Schema.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// <c>umbraco deploy schema show</c>: shows the status of a schema deployment (or the current one), optionally waiting for it.
/// </summary>
internal sealed class SchemaShowCommandHandler
{
    private readonly IEnvironment _environment;
    private readonly IDeployApiClient _deployApiClient;
    private readonly SchemaDeploymentWaiter _waiter;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public SchemaShowCommandHandler(IEnvironment environment, IDeployApiClient deployApiClient, SchemaDeploymentWaiter waiter, IOutput output, IReporter reporter)
    {
        _environment = environment;
        _deployApiClient = deployApiClient;
        _waiter = waiter;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(SchemaShowSettings settings, CancellationToken cancellationToken)
    {
        SchemaTarget target = SchemaTargetResolver.Resolve(settings.Target, _environment);
        _reporter.Verbose($"Using the credentials from {target.Credentials.Source} for {target.Url}.");

        WorkStatusResponse status;
        try
        {
            status = await _deployApiClient.GetStatusAsync(target, settings.TaskId, cancellationToken);
        }
        catch (DeployApiException exception) when (settings.TaskId is not null && exception.StatusCode is not null && exception.IsTransient)
        {
            throw new CliException(exception.Message, exception.ExitCode, "The site may not know this schema deployment (any more): Umbraco Deploy only keeps the status until the site restarts.", exception);
        }

        if (status.IsKnown is false)
        {
            string which = settings.TaskId is Guid id ? $"schema deployment {id}" : "current schema deployment";
            throw new CliException($"Umbraco Deploy on {target.DisplayName} doesn't know the {which}.", ExitCodes.Error, "Umbraco Deploy only keeps the status until the site restarts.");
        }

        Guid taskId = status.Id;
        bool restarted = false;
        if (settings.Wait && status.IsTerminal is false)
        {
            SchemaWaitResult result = await _waiter.WaitAsync(target, taskId, settings.Timeout, restart: null, cancellationToken);
            (taskId, status, restarted) = (result.TaskId, result.Status, result.Restarted);
        }

        return SchemaDeploymentResultWriter.Write(SchemaDeploymentResult.From(target, taskId, status, restarted), target.DisplayName, _output, _reporter);
    }
}
