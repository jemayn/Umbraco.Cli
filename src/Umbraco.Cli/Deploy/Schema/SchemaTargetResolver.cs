using System.Text;
using System.Text.Json;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// Resolves the Environment and credentials of a schema deployment from options, environment variables and the Umbraco project's configuration files.
/// </summary>
/// <remarks>
/// The credentials come from the first source that has an ApiKey or ApiSecret: the environment variables (secrets are never options, see ADR 0009), <c>umbraco-cloud.json</c>
/// (<c>Deploy:Settings</c>) and <c>appsettings.json</c> (<c>Umbraco:Deploy:Settings</c>). Within a source the obsolete ApiKey wins, as it does in Umbraco Deploy.
/// An Environment name is looked up in the <c>Workspaces</c> of <c>umbraco-cloud.json</c>, then of <c>appsettings.json</c>. A single file can be given
/// instead of a folder: <c>umbraco-cloud.json</c>, or any other file as app settings (such as <c>appsettings.Production.json</c>).
/// </remarks>
internal static class SchemaTargetResolver
{
    public const string ApiSecretVariable = "UMBRACO_DEPLOY_API_SECRET";

    public const string ApiKeyVariable = "UMBRACO_DEPLOY_API_KEY";

    public const string AppSettingsFileName = "appsettings.json";

    public const string UmbracoCloudFileName = "umbraco-cloud.json";

    private const string ConfigHint = "Use --config with the appsettings.json or umbraco-cloud.json of the Umbraco project, or the folder containing it.";

    private static readonly string[] CloudSettings = ["Deploy", "Settings"];
    private static readonly string[] CloudWorkspaces = ["Deploy", "Project", "Workspaces"];
    private static readonly string[] AppSettingsSettings = ["Umbraco", "Deploy", "Settings"];
    private static readonly string[] AppSettingsWorkspaces = ["Umbraco", "Deploy", "Project", "Workspaces"];

    public static SchemaTarget Resolve(SchemaTargetSettings settings, IEnvironment environment)
    {
        string config = Path.GetFullPath(settings.Config ?? FindConfig(environment.CurrentDirectory), environment.CurrentDirectory);
        JsonDocumentEditor? cloud;
        JsonDocumentEditor? appSettings;
        if (File.Exists(config))
        {
            bool isCloud = string.Equals(Path.GetFileName(config), UmbracoCloudFileName, StringComparison.OrdinalIgnoreCase);
            cloud = isCloud ? Load(config) : null;
            appSettings = isCloud ? null : Load(config);
        }
        else if (Directory.Exists(config))
        {
            cloud = Load(Path.Combine(config, UmbracoCloudFileName));
            appSettings = Load(Path.Combine(config, AppSettingsFileName));
        }
        else
        {
            throw new UsageException($"'{config}' doesn't exist.", ConfigHint);
        }

        (Uri baseUrl, string? name) = ResolveUrl(settings, cloud, appSettings, config);
        return new SchemaTarget(baseUrl, name, ResolveCredentials(environment, cloud, appSettings));
    }

    /// <summary>
    /// Gets the current directory when it has configuration files, else the directory of the Umbraco project in its repository (when there's one with an
    /// <c>umbraco-cloud.json</c>), else the current directory.
    /// </summary>
    private static string FindConfig(string currentDirectory)
    {
        if (File.Exists(Path.Combine(currentDirectory, AppSettingsFileName)) || File.Exists(Path.Combine(currentDirectory, UmbracoCloudFileName)))
        {
            return currentDirectory;
        }

        return UmbracoCloudJsonProjectId.Discover(currentDirectory) is string discovered ? Path.GetDirectoryName(discovered)! : currentDirectory;
    }

    /// <returns>The base URL, and the name of the Environment when it wasn't given by URL.</returns>
    private static (Uri BaseUrl, string? Name) ResolveUrl(SchemaTargetSettings settings, JsonDocumentEditor? cloud, JsonDocumentEditor? appSettings, string config)
    {
        if (settings.Url is string url)
        {
            return (ToBaseUrl(url, "--url"), null);
        }

        WorkspaceEntry[] workspaces =
        [
            .. WorkspaceList.Read(cloud?.GetNode(CloudWorkspaces)),
            .. WorkspaceList.Read(appSettings?.GetNode(AppSettingsWorkspaces)),
        ];
        string[] names = workspaces.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string configured = names.Length == 0 ? $"No Environments are configured in '{config}'" : $"The configured Environments are {string.Join(", ", names)}";

        // A single configured Environment is the default target
        string name = settings.Environment ?? (names.Length == 1
            ? names[0]
            : throw new UsageException($"No target Environment was specified. {configured}.", "Use --environment or --url."));

        WorkspaceEntry workspace = workspaces.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException(
                $"No Environment named '{name}' is configured. {configured}.",
                "Use --config with the folder of the Umbraco project, --url with the Environment's URL, or 'umbraco deploy init --environment' to configure it.");

        return (ToBaseUrl(workspace.Url, $"Environment '{workspace.Name}'"), name);
    }

    private static Uri ToBaseUrl(string url, string source)
    {
        if (EnvironmentSpec.GetUrlError(url) is string error)
        {
            throw new UsageException($"The URL of {source} is invalid: {error}.");
        }

        // A trailing slash keeps a path base (such as https://example.com/site/) when combining it with the API paths
        return new Uri(url.TrimEnd('/') + "/");
    }

    private static DeployCredentials ResolveCredentials(IEnvironment environment, JsonDocumentEditor? cloud, JsonDocumentEditor? appSettings)
    {
        (string Source, string? ApiKey, string? ApiSecret)[] sources =
        [
            (environment.GetVariable(ApiKeyVariable) is { Length: > 0 } ? ApiKeyVariable : ApiSecretVariable, environment.GetVariable(ApiKeyVariable), environment.GetVariable(ApiSecretVariable)),
            (UmbracoCloudFileName, cloud?.GetString([.. CloudSettings, "ApiKey"]), cloud?.GetString([.. CloudSettings, "ApiSecret"])),
            (AppSettingsFileName, appSettings?.GetString([.. AppSettingsSettings, "ApiKey"]), appSettings?.GetString([.. AppSettingsSettings, "ApiSecret"])),
        ];

        foreach ((string source, string? apiKey, string? apiSecret) in sources)
        {
            if (string.IsNullOrWhiteSpace(apiKey) is false)
            {
                return new DeployCredentials(Encoding.UTF8.GetBytes(apiKey.Trim()), source);
            }

            if (string.IsNullOrWhiteSpace(apiSecret) is false)
            {
                try
                {
                    return new DeployCredentials(Convert.FromBase64String(apiSecret.Trim()), source);
                }
                catch (FormatException)
                {
                    throw new UsageException($"The ApiSecret from {source} isn't valid Base64.", "Use the ApiSecret as configured for Umbraco Deploy on the Environment.");
                }
            }
        }

        throw new UsageException(
            "No ApiSecret for Umbraco Deploy was specified.",
            $"Set the {ApiSecretVariable} environment variable, or use --config with the {AppSettingsFileName} or {UmbracoCloudFileName} of the Umbraco project (or the folder containing it). {LocalEnvironmentFile.PullHint}");
    }

    private static JsonDocumentEditor? Load(string path)
    {
        if (File.Exists(path) is false)
        {
            return null;
        }

        try
        {
            return JsonDocumentEditor.Parse(File.ReadAllBytes(path));
        }
        catch (JsonException exception)
        {
            throw new CliException($"'{path}' is not a valid JSON object: {exception.Message}", ExitCodes.Error, innerException: exception);
        }
    }
}
