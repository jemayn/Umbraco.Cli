namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The placeholder Umbraco ID values for the <c>Identity</c> block of a new <c>umbraco-cloud.json</c> (see ADR 0008).
/// </summary>
/// <remarks>
/// The CLI never writes the real Umbraco ID values: the client secret is a secret, and secrets for Umbraco Cloud
/// are provisioned by Umbraco Cloud rather than written to files that are committed.
/// </remarks>
internal static class IdentityValues
{
    public const string PlaceholderClientId = "00000000-0000-0000-0000-000000000000";

    public const string PlaceholderClientSecret = "local-development-placeholder";
}
