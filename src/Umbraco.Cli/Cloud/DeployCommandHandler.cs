using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// <c>umbraco cloud deployments deploy</c>: starts a Deployment (uploading the Artifact first when needed) and waits for it.
/// </summary>
internal sealed class DeployCommandHandler
{
    public const string DefaultCommitMessage = "Deployed with Umbraco CLI";

    private readonly IEnvironment _environment;
    private readonly ICloudApiClient _cloudApiClient;
    private readonly ArtifactUploader _uploader;
    private readonly DeploymentWaiter _waiter;
    private readonly IGit _git;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public DeployCommandHandler(
        IEnvironment environment,
        ICloudApiClient cloudApiClient,
        ArtifactUploader uploader,
        DeploymentWaiter waiter,
        IGit git,
        IOutput output,
        IReporter reporter)
    {
        _environment = environment;
        _cloudApiClient = cloudApiClient;
        _uploader = uploader;
        _waiter = waiter;
        _git = git;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(DeployCommandSettings settings, CancellationToken cancellationToken)
    {
        CloudCredentials credentials = CloudCredentialsResolver.Resolve(settings.ProjectId, _environment, settings.Config, settings.Source?.Directory);

        string? uploadedArtifactId = null;
        string artifactId;
        if (settings.ArtifactId is not null)
        {
            artifactId = settings.ArtifactId;
        }
        else
        {
            ArtifactResponse artifact = await _uploader.UploadAsync(credentials, settings.Source!, requireDotUmbraco: settings.SkipPreserveUmbracoCloudJson is false, cancellationToken);
            _reporter.Info($"Artifact {artifact.ArtifactId} uploaded.");
            artifactId = uploadedArtifactId = artifact.ArtifactId;
        }

        string commitMessage = settings.Message
            ?? await _git.GetHeadSubjectAsync(_environment.CurrentDirectory, cancellationToken)
            ?? DefaultCommitMessage;

        var request = new StartDeploymentRequest
        {
            ArtifactId = artifactId,
            TargetEnvironmentAlias = settings.Environment,
            CommitMessage = commitMessage,
            NoBuildAndRestore = settings.NoBuildAndRestore,
            SkipVersionCheck = settings.SkipVersionCheck,
            SkipPreserveUmbracoCloudJson = settings.SkipPreserveUmbracoCloudJson,
            RunSchemaExtraction = settings.NoSchemaExtraction is false,
        };

        DeploymentResponse deployment = await _cloudApiClient.StartDeploymentAsync(credentials, request, cancellationToken);
        _reporter.Info($"Deployment {deployment.DeploymentId} to {settings.Environment} started.");

        if (settings.NoWait is false && deployment.IsTerminal is false)
        {
            deployment = await _waiter.WaitAsync(credentials, deployment.DeploymentId, settings.Timeout, cancellationToken);
        }

        return DeploymentResultWriter.Write(deployment with { ArtifactId = uploadedArtifactId }, _output, _reporter);
    }
}
