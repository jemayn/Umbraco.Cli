using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// Prepares and uploads an Artifact, shared by the <c>upload</c> and <c>deploy</c> commands.
/// </summary>
internal sealed class ArtifactUploader
{
    /// <summary>
    /// The maximum size of an Artifact accepted by the CI/CD API v2 (256 MiB; Cloudflare in front of the API allows 500 MB).
    /// </summary>
    public const long DefaultMaxArtifactSize = 256L * 1024 * 1024;

    private readonly ICloudApiClient _cloudApiClient;
    private readonly ArtifactZipBuilder _zipBuilder;
    private readonly IReporter _reporter;

    public ArtifactUploader(ICloudApiClient cloudApiClient, ArtifactZipBuilder zipBuilder, IReporter reporter)
    {
        _cloudApiClient = cloudApiClient;
        _zipBuilder = zipBuilder;
        _reporter = reporter;
    }

    /// <summary>
    /// Gets the maximum size of an Artifact (settable for tests).
    /// </summary>
    internal long MaxArtifactSize { get; init; } = DefaultMaxArtifactSize;

    /// <summary>
    /// Creates (when needed), checks and uploads an Artifact. When <c>requireDotUmbraco</c> is set, a Git deployment without a <c>.umbraco</c> file fails before uploading (instead of only warning).
    /// </summary>
    public async Task<ArtifactResponse> UploadAsync(CloudCredentials credentials, ArtifactSource source, bool requireDotUmbraco, CancellationToken cancellationToken)
    {
        using ArtifactPackage package = await PrepareAsync(source, cancellationToken);
        if (package.Type == DeploymentType.Git && package.HasDotUmbraco is false)
        {
            const string message = "The source code has no .umbraco file at its root, which Umbraco Cloud needs to preserve umbraco-cloud.json in a Git deployment.";
            const string hint = "Add the .umbraco file from your Umbraco Cloud repository, or deploy with --skip-preserve-umbraco-cloud-json.";
            if (requireDotUmbraco)
            {
                throw new UsageException(message, hint);
            }

            _reporter.Warning(message, hint);
        }

        long length = package.Length;
        if (length > MaxArtifactSize)
        {
            throw new UsageException($"The artifact is {ByteSize.Format(length)}, but Umbraco Cloud accepts at most {ByteSize.Format(MaxArtifactSize)}.");
        }

        string description = source.File is not null ? Path.GetFileName(source.File) : "artifact";
        return await _reporter.TrackUploadAsync(
            $"Uploading {description}",
            length,
            progress => _cloudApiClient.UploadArtifactAsync(credentials, package.ZipPath, source.Description, source.Version, progress, cancellationToken));
    }

    private async Task<ArtifactPackage> PrepareAsync(ArtifactSource source, CancellationToken cancellationToken)
    {
        if (source.File is not null)
        {
            if (File.Exists(source.File) is false)
            {
                throw new UsageException($"The file '{source.File}' does not exist.");
            }

            if (string.Equals(Path.GetExtension(source.File), ".zip", StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new UsageException($"The file '{source.File}' must be a .zip file.");
            }

            return new ArtifactPackage(Path.GetFullPath(source.File), type: null, hasDotUmbraco: true, isTemporary: false);
        }

        _reporter.Verbose($"Creating the artifact from '{source.Directory}'.");
        ArtifactPackage package = await _zipBuilder.CreateAsync(source.Directory!, source.Type, cancellationToken);
        string kind = package.Type == DeploymentType.Git ? "a Git deployment (source code)" : "an Artifact deployment (published output)";
        _reporter.Info($"Zipped '{source.Directory}' for {kind}: {ByteSize.Format(package.Length)}.");
        if (package.ExcludedFiles.Count > 0)
        {
            const int listed = 5;
            string more = package.ExcludedFiles.Count > listed ? $" and {package.ExcludedFiles.Count - listed} more" : string.Empty;
            _reporter.Info($"Left out files that are never deployed (environment files, local databases, logs and temporary files): {string.Join(", ", package.ExcludedFiles.Take(listed))}{more}.");
        }

        return package;
    }
}
