namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The options of <c>umbraco cloud init</c>.
/// </summary>
internal sealed record CloudInitSettings
{
    public string? Project { get; init; }

    /// <summary>
    /// Gets <c>artifact</c> (published output, the default) or <c>git</c> (source code): how the Umbraco project is deployed to Umbraco Cloud.
    /// </summary>
    public string DeploymentType { get; init; } = "artifact";

    public bool IsGitDeployment => DeploymentType == "git";

    public string? Name { get; init; }

    public string? Output { get; init; }

    public string? Version { get; init; }

    /// <summary>
    /// Gets <c>ide</c> or <c>backoffice</c> (the <c>umbraco</c> template's development mode), or <c>null</c> for the default (<c>ide</c>).
    /// </summary>
    public string? DevelopmentMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether to create no <c>.gitignore</c> files (also passed to the <c>umbraco</c> template).
    /// </summary>
    public bool ExcludeGitignore { get; init; }

    public IReadOnlyList<string> Add { get; init; } = [];

    public bool AllowEndOfLife { get; init; }

    public bool NoBuild { get; init; }

    public bool NonInteractive { get; init; }

    public IReadOnlyList<string> TemplateOptions { get; init; } = [];
}
