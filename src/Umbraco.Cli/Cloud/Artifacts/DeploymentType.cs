namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// What an Artifact contains, which decides how Umbraco Cloud deploys it.
/// </summary>
internal enum DeploymentType
{
    /// <summary>
    /// Source code that Umbraco Cloud commits to the project's git repository and builds.
    /// </summary>
    Git,

    /// <summary>
    /// Already-published output that Umbraco Cloud runs as-is.
    /// </summary>
    Artifact,
}
