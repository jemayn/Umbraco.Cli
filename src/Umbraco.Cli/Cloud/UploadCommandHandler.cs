using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// <c>umbraco cloud deployments upload</c>: uploads an Artifact.
/// </summary>
internal sealed class UploadCommandHandler
{
    private readonly IEnvironment _environment;
    private readonly ArtifactUploader _uploader;
    private readonly IOutput _output;

    public UploadCommandHandler(IEnvironment environment, ArtifactUploader uploader, IOutput output)
    {
        _environment = environment;
        _uploader = uploader;
        _output = output;
    }

    public async Task<int> HandleAsync(UploadCommandSettings settings, CancellationToken cancellationToken)
    {
        CloudCredentials credentials = CloudCredentialsResolver.Resolve(settings.ProjectId, _environment, settings.Config, settings.Source.Directory);
        ArtifactResponse artifact = await _uploader.UploadAsync(credentials, settings.Source, requireDotUmbraco: false, cancellationToken);

        _output.Write(artifact, CliJsonContext.Default.ArtifactResponse, result => $"Artifact {result.ArtifactId} uploaded ({ByteSize.Format(result.FileSize)}).");
        return ExitCodes.Success;
    }
}
