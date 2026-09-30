namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The result of <c>umbraco cloud init</c>, as written with <c>--output json</c> (a public contract: don't rename properties).
/// </summary>
internal sealed record CloudInitResult
{
    public required bool Created { get; init; }

    /// <summary>
    /// Gets <c>artifact</c> or <c>git</c>.
    /// </summary>
    public required string DeploymentType { get; init; }

    public required string RepositoryRoot { get; init; }

    public required string ProjectPath { get; init; }

    public required string CmsVersion { get; init; }

    /// <summary>
    /// Gets <c>LTS</c> or <c>STS</c>.
    /// </summary>
    public required string ReleaseType { get; init; }

    /// <summary>
    /// Gets <c>prerelease</c>, <c>active</c>, <c>support</c>, <c>security</c> or <c>end-of-life</c>.
    /// </summary>
    public required string LifecyclePhase { get; init; }

    public required IReadOnlyList<CloudInitPackage> Packages { get; init; }

    public required IReadOnlyList<string> FilesWritten { get; init; }

    /// <summary>
    /// Gets <c>placeholder</c>, <c>provided</c> or <c>unchanged</c> (the ClientSecret itself is never written to output).
    /// </summary>
    public required string Identity { get; init; }

    public required bool Built { get; init; }
}
