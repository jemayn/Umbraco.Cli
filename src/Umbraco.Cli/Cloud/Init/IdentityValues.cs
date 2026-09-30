using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The Umbraco ID values for the <c>Identity</c> block of <c>umbraco-cloud.json</c> (see ADR 0003).
/// </summary>
internal sealed record IdentityValues(string ClientId, string ClientSecret, bool IsPlaceholder)
{
    public const string ClientIdVariable = "UMBRACO_CLOUD_IDENTITY_CLIENT_ID";

    public const string ClientSecretVariable = "UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET";

    public const string PlaceholderClientId = "00000000-0000-0000-0000-000000000000";

    public const string PlaceholderClientSecret = "local-development-placeholder";

    /// <summary>
    /// Gets the values from the options, else the environment variables, else placeholders; both or neither must be given.
    /// </summary>
    /// <returns>The values, or <c>null</c> when none were given (placeholders apply to new files only).</returns>
    public static IdentityValues? Resolve(string? clientId, string? clientSecret, IEnvironment environment)
    {
        string? id = FirstNonEmpty(clientId, environment.GetVariable(ClientIdVariable));
        string? secret = FirstNonEmpty(clientSecret, environment.GetVariable(ClientSecretVariable));
        if (id is null && secret is null)
        {
            return null;
        }

        if (id is null || secret is null)
        {
            throw new UsageException(
                "Specify both the Umbraco ID client ID and client secret, or neither.",
                $"Use --identity-client-id and --identity-client-secret, or {ClientIdVariable} and {ClientSecretVariable}.");
        }

        return new IdentityValues(id, secret, IsPlaceholder: false);
    }

    public static IdentityValues Placeholder { get; } = new(PlaceholderClientId, PlaceholderClientSecret, IsPlaceholder: true);

    // Never print the client secret
    public override string ToString() => $"IdentityValues {{ ClientId = {ClientId}, IsPlaceholder = {IsPlaceholder} }}";

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => string.IsNullOrWhiteSpace(value) is false)?.Trim();
}
