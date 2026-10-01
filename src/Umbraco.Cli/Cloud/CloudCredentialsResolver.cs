using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Resolves the Umbraco Cloud credentials from options, falling back to environment variables (they are never stored) and, for the project ID,
/// to <c>umbraco-cloud.json</c> (<c>--config</c>, or the uploaded directory).
/// </summary>
internal static class CloudCredentialsResolver
{
    public const string ProjectIdVariable = "UMBRACO_CLOUD_PROJECT_ID";

    public const string ApiKeyVariable = "UMBRACO_CLOUD_API_KEY";

    /// <param name="projectId">The <c>--project-id</c> option.</param>
    /// <param name="apiKey">The <c>--api-key</c> option.</param>
    /// <param name="environment">The environment, for the environment variables.</param>
    /// <param name="config">The <c>umbraco-cloud.json</c> that can give the project ID, or a directory containing it (<c>--config</c>, which must exist).</param>
    /// <param name="uploadDirectory">The uploaded directory (<c>--path</c>), used instead of <paramref name="config" /> when it isn't given.</param>
    public static CloudCredentials Resolve(string? projectId, string? apiKey, IEnvironment environment, string? config = null, string? uploadDirectory = null)
    {
        // A missing upload directory is reported when it's uploaded, while a missing --config is an error here
        if (config is null && uploadDirectory is not null && Directory.Exists(Path.GetFullPath(uploadDirectory, environment.CurrentDirectory)))
        {
            config = uploadDirectory;
        }

        string? resolvedProjectId = FirstNonEmpty(projectId, environment.GetVariable(ProjectIdVariable))
            ?? (config is null ? null : UmbracoCloudJsonProjectId.Find(Path.GetFullPath(config, environment.CurrentDirectory)));
        string? resolvedApiKey = FirstNonEmpty(apiKey, environment.GetVariable(ApiKeyVariable));

        if (resolvedProjectId is null)
        {
            throw new UsageException(
                "No Umbraco Cloud project ID was specified.",
                config is null
                    ? $"Use --project-id, set the {ProjectIdVariable} environment variable, or use --config with the {UmbracoCloudJsonProjectId.FileName} of the Umbraco project."
                    : $"Use --project-id, set the {ProjectIdVariable} environment variable, or set Deploy:Project:Id in '{config}' (for example with 'umbraco deploy init --project-id').");
        }

        if (resolvedApiKey is null)
        {
            throw new UsageException("No Umbraco Cloud CI/CD API key was specified.", $"Use --api-key or set the {ApiKeyVariable} environment variable.");
        }

        return new CloudCredentials(resolvedProjectId, resolvedApiKey);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => string.IsNullOrWhiteSpace(value) is false)?.Trim();
}
