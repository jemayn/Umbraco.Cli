namespace Umbraco.Cli.Releases;

/// <summary>
/// Whether an Umbraco major is a long-term or short-term supported release.
/// </summary>
internal enum ReleaseType
{
    /// <summary>
    /// Short-term support.
    /// </summary>
    Sts,

    /// <summary>
    /// Long-term support.
    /// </summary>
    Lts,
}
