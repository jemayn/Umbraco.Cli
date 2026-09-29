namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// A zip file ready to upload; a temporary zip is deleted when disposed.
/// </summary>
internal sealed class ArtifactPackage : IDisposable
{
    public ArtifactPackage(string zipPath, DeploymentType? type, bool hasDotUmbraco, bool isTemporary)
    {
        ZipPath = zipPath;
        Type = type;
        HasDotUmbraco = hasDotUmbraco;
        IsTemporary = isTemporary;
    }

    public string ZipPath { get; }

    /// <summary>
    /// Gets the deployment type, or <c>null</c> when an existing zip is uploaded as-is.
    /// </summary>
    public DeploymentType? Type { get; }

    /// <summary>
    /// Gets a value indicating whether the root contains the <c>.umbraco</c> file that Git deployments need.
    /// </summary>
    public bool HasDotUmbraco { get; }

    public bool IsTemporary { get; }

    public long Length => new FileInfo(ZipPath).Length;

    public void Dispose()
    {
        if (IsTemporary && File.Exists(ZipPath))
        {
            File.Delete(ZipPath);
        }
    }
}
