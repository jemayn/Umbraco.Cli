namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// The result of <c>umbraco deploy init</c>, as written with <c>--output json</c>.
/// </summary>
internal sealed record InitResult
{
    public required string Package { get; init; }

    public required string PackageVersion { get; init; }

    public required string File { get; init; }

    public required bool DryRun { get; init; }

    public required IReadOnlyList<WorkspaceEntry> Environments { get; init; }

    public required IReadOnlyList<string> Changes { get; init; }

    /// <summary>
    /// Gets a value indicating whether a new ApiSecret was generated (Umbraco Deploy On-Premises only); secrets themselves are never part of the output.
    /// </summary>
    public required bool ApiSecretGenerated { get; init; }

    /// <summary>
    /// Gets the configured Baseline URL (or, on Umbraco Cloud, the one that Umbraco Cloud provisioned).
    /// </summary>
    public string? BaselineUrl { get; init; }

    public bool BaselineSecretGenerated { get; init; }

    public bool ChildSecretGenerated { get; init; }

    /// <summary>
    /// Gets the name of the newly generated child secret.
    /// </summary>
    public string? ChildSecretName { get; init; }

    public bool BaselineSecretConfigured { get; init; }

    public int ChildSecretCount { get; init; }
}
