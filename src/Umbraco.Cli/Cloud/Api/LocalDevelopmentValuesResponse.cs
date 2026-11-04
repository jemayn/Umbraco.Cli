namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// The configuration values and secrets of a project for local development.
/// </summary>
/// <remarks>
/// Provisional: the Umbraco Cloud endpoint isn't available yet, so this shape is a guess to be aligned with it (see ADR 0010).
/// </remarks>
internal sealed record LocalDevelopmentValuesResponse
{
    /// <summary>
    /// Gets the values by environment variable name (or configuration key, with <c>:</c> separators).
    /// </summary>
    public Dictionary<string, string> Values { get; init; } = [];
}
