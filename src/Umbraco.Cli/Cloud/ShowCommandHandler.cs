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
        CloudCredentials credentials = CloudCredentialsResolver.Resolve(settings.ProjectId, settings.ApiKey, _environment);
        DeploymentResponse deployment = settings.Wait
            ? await _waiter.WaitAsync(credentials, settings.DeploymentId, settings.Timeout, cancellationToken)
            : await _cloudApiClient.GetDeploymentAsync(credentials, settings.DeploymentId, lastModifiedUtc: null, cancellationToken);

        return DeploymentResultWriter.Write(deployment, _output, _reporter);
    }
}
