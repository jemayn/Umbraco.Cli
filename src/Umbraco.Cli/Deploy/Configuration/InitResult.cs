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

    public required bool ApiSecretGenerated { get; init; }

    /// <summary>
    /// Gets the newly generated ApiSecret (not set for a dry run, or when no ApiSecret was generated).
    /// </summary>
    public string? ApiSecret { get; init; }

    /// <summary>
    /// Gets the configured Baseline URL (or, on Umbraco Cloud, the one that Umbraco Cloud provisioned).
    /// </summary>
    public string? BaselineUrl { get; init; }

    public bool BaselineSecretGenerated { get; init; }

    /// <summary>
    /// Gets the newly generated Baseline secret (not set for a dry run, or when none was generated).
    /// </summary>
    public string? BaselineSecret { get; init; }

    public bool ChildSecretGenerated { get; init; }

    /// <summary>
    /// Gets the name of the newly generated child secret.
    /// </summary>
    public string? ChildSecretName { get; init; }

    /// <summary>
    /// Gets the newly generated child secret (not set for a dry run, or when none was generated).
    /// </summary>
    public string? ChildSecret { get; init; }

    public bool BaselineSecretConfigured { get; init; }

    public int ChildSecretCount { get; init; }
}
