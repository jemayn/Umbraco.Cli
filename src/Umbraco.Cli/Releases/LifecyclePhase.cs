namespace Umbraco.Cli.Releases;

/// <summary>
/// Where an Umbraco major is in its support life.
/// </summary>
internal enum LifecyclePhase
{
    /// <summary>
    /// Only prereleases have been published.
    /// </summary>
    Prerelease,

    /// <summary>
    /// New features and fixes (the first 6 months).
    /// </summary>
    Active,

    /// <summary>
    /// Bug and security fixes.
    /// </summary>
    Support,

    /// <summary>
    /// Security fixes only.
    /// </summary>
    Security,

    /// <summary>
    /// No longer supported.
    /// </summary>
    EndOfLife,
}
