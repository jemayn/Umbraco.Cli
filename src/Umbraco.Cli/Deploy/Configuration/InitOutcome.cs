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
    /// Gets or sets the Baseline secret that was generated, if any.
    /// </summary>
    public string? GeneratedBaselineSecret { get; set; }

    /// <summary>
    /// Gets or sets the child secret that was generated, if any.
    /// </summary>
    public string? GeneratedChildSecret { get; set; }

    /// <summary>
    /// Gets or sets the name of the child secret that was generated, if any.
    /// </summary>
    public string? GeneratedChildSecretName { get; set; }

    /// <summary>
    /// Gets or sets the configured Baseline URL after the changes.
    /// </summary>
    public string? BaselineUrl { get; set; }

    /// <summary>
    /// Gets or sets whether a Baseline secret is configured after the changes.
    /// </summary>
    public bool BaselineSecretConfigured { get; set; }

    /// <summary>
    /// Gets or sets the number of configured child secrets after the changes.
    /// </summary>
    public int ChildSecretCount { get; set; }

    /// <summary>
    /// Gets or sets the configured Environments after the changes.
    /// </summary>
    public IReadOnlyList<WorkspaceEntry> Environments { get; set; } = [];
}
