using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// <c>umbraco cloud deployments show</c>: shows the status of a Deployment, optionally waiting for it.
/// </summary>
internal sealed class ShowCommandHandler
{
    private readonly IEnvironment _environment;
    private readonly ICloudApiClient _cloudApiClient;
    private readonly DeploymentWaiter _waiter;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public ShowCommandHandler(IEnvironment environment, ICloudApiClient cloudApiClient, DeploymentWaiter waiter, IOutput output, IReporter reporter)
    {
        _environment = environment;
        _cloudApiClient = cloudApiClient;
        _waiter = waiter;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(ShowCommandSettings settings, CancellationToken cancellationToken)
    {
        CloudCredentials credentials = CloudCredentialsResolver.Resolve(settings.ProjectId, _environment, settings.Config);
        string deploymentId = settings.DeploymentId ?? await GetLatestDeploymentIdAsync(credentials, settings.Environment, cancellationToken);
        DeploymentResponse deployment = settings.Wait
            ? await _waiter.WaitAsync(credentials, deploymentId, settings.Timeout, cancellationToken)
            : await _cloudApiClient.GetDeploymentAsync(credentials, deploymentId, lastModifiedUtc: null, cancellationToken);

        return DeploymentResultWriter.Write(deployment, _output, _reporter);
    }

    private async Task<string> GetLatestDeploymentIdAsync(CloudCredentials credentials, string? environmentAlias, CancellationToken cancellationToken)
    {
        string? deploymentId = await _cloudApiClient.GetLatestDeploymentIdAsync(credentials, environmentAlias, cancellationToken);
        string target = environmentAlias is null ? string.Empty : $" to {environmentAlias}";
        if (deploymentId is null)
        {
            throw new CliException($"Umbraco Cloud has no deployments{target} for this project.", ExitCodes.Error, "Only deployments started through the CI/CD API are listed.");
        }

        _reporter.Info($"Showing the latest deployment{target}, {deploymentId}.");
        return deploymentId;
    }
}
