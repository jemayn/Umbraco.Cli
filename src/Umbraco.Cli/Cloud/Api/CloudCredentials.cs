namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// Identifies an Umbraco Cloud project and the CI/CD API key that is scoped to it.
/// </summary>
internal sealed record CloudCredentials(string ProjectId, string ApiKey)
{
    // Never print the API key, not even in debug output
    public override string ToString() => $"CloudCredentials {{ ProjectId = {ProjectId} }}";
}
