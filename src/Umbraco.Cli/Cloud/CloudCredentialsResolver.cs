using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Resolves the Umbraco Cloud credentials: the project ID from the option, the environment variable or <c>umbraco-cloud.json</c>
/// (<c>--config</c>, or the uploaded directory), and the CI/CD API key from the environment variable only, as secrets are never given as options (see ADR 0009).
/// </summary>
internal static class CloudCredentialsResolver
{
    public const string ProjectIdVariable = "UMBRACO_CLOUD_PROJECT_ID";

    public const string ApiKeyVariable = "UMBRACO_CLOUD_API_KEY";

    /// <summary>
    /// Explains where to find the CI/CD API key and how to set it.
    /// </summary>
    public const string ApiKeyHint = $"Set the {ApiKeyVariable} environment variable to the project's CI/CD API key, from the Umbraco Cloud Portal (Configuration > CI/CD Flow).";

    /// <param name="projectId">The <c>--project-id</c> option.</param>
    /// <param name="environment">The environment, for the environment variables.</param>
    /// <param name="config">The <c>umbraco-cloud.json</c> that can give the project ID, or a directory containing it (<c>--config</c>, which must exist).</param>
    /// <param name="uploadDirectory">The uploaded directory (<c>--path</c>), used instead of <paramref name="config" /> when it isn't given.</param>
    public static CloudCredentials Resolve(string? projectId, IEnvironment environment, string? config = null, string? uploadDirectory = null)
    {
        // A missing upload directory is reported when it's uploaded, while a missing --config is an error here
        if (config is null && uploadDirectory is not null && Directory.Exists(Path.GetFullPath(uploadDirectory, environment.CurrentDirectory)))
        {
            config = uploadDirectory;
        }

        string? resolvedProjectId = FirstNonEmpty(projectId, environment.GetVariable(ProjectIdVariable))
            ?? (config is null ? null : UmbracoCloudJsonProjectId.Find(Path.GetFullPath(config, environment.CurrentDirectory)));
        string? resolvedApiKey = FirstNonEmpty(environment.GetVariable(ApiKeyVariable));

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
            throw new UsageException($"The {ApiKeyVariable} environment variable isn't set.", ApiKeyHint);
        }

        return new CloudCredentials(resolvedProjectId, resolvedApiKey);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => string.IsNullOrWhiteSpace(value) is false)?.Trim();
}
