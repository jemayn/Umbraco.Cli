using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Resolves the Umbraco Cloud credentials from options, falling back to environment variables (they are never stored).
/// </summary>
internal static class CloudCredentialsResolver
{
    public const string ProjectIdVariable = "UMBRACO_CLOUD_PROJECT_ID";

    public const string ApiKeyVariable = "UMBRACO_CLOUD_API_KEY";

    public static CloudCredentials Resolve(string? projectId, string? apiKey, IEnvironment environment)
    {
        string? resolvedProjectId = FirstNonEmpty(projectId, environment.GetVariable(ProjectIdVariable));
        string? resolvedApiKey = FirstNonEmpty(apiKey, environment.GetVariable(ApiKeyVariable));

        if (resolvedProjectId is null)
        {
            throw new UsageException("No Umbraco Cloud project ID was specified.", $"Use --project-id or set the {ProjectIdVariable} environment variable.");
        }

        if (resolvedApiKey is null)
        {
            throw new UsageException("No Umbraco Cloud CI/CD API key was specified.", $"Use --api-key or set the {ApiKeyVariable} environment variable.");
        }

        return new CloudCredentials(resolvedProjectId, resolvedApiKey);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => string.IsNullOrWhiteSpace(value) is false)?.Trim();
}
