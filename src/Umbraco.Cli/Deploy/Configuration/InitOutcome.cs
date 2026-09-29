namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// The changes a configuration writer made (or would make, in a dry run).
/// </summary>
internal sealed class InitOutcome
{
    public List<string> Changes { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Notes { get; } = [];

    /// <summary>
    /// Gets or sets the ApiSecret that was generated, if any.
    /// </summary>
    public string? GeneratedApiSecret { get; set; }

    /// <summary>
    /// Gets or sets the configured Environments after the changes.
    /// </summary>
    public IReadOnlyList<WorkspaceEntry> Environments { get; set; } = [];
}
