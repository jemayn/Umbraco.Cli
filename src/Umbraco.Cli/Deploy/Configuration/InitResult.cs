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
}
