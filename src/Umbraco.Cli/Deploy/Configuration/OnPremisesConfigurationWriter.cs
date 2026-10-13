using System.Text.Json.Nodes;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// Configures Umbraco Deploy On-Premises in <c>appsettings.json</c> (section <c>Umbraco:Deploy</c>).
/// </summary>
internal sealed class OnPremisesConfigurationWriter : IDeployConfigurationWriter
{
    private static readonly string[] Settings = ["Umbraco", "Deploy", "Settings"];
    private static readonly string[] Project = ["Umbraco", "Deploy", "Project"];

    private readonly Func<string> _generateSecret;
    private readonly Func<Guid> _newId;

    public OnPremisesConfigurationWriter(Func<string> generateSecret, Func<Guid> newId)
    {
        _generateSecret = generateSecret;
        _newId = newId;
    }

    public static bool HasBaselineUrl(JsonDocumentEditor document)
        => string.IsNullOrEmpty(document.GetString([.. Settings, "BaselineUrl"])) is false;

    public DeployPackage Package => DeployPackage.OnPremises;

    public string FileName => "appsettings.json";

    public InitOutcome Apply(JsonDocumentEditor document, InitRequest request)
    {
        var outcome = new InitOutcome();

        string[] workspacesPath = [.. Project, "Workspaces"];
        IReadOnlyList<WorkspaceEntry> existing = WorkspaceList.Read(document.GetNode(workspacesPath));
        outcome.Environments = existing;
        if (request.Environments is not null)
        {
            outcome.Environments = WorkspaceList.Build(existing, request.Environments, _newId);
            document.Set(workspacesPath, WorkspaceList.ToJson(outcome.Environments));
            outcome.Changes.Add($"Set the environments to {string.Join(", ", outcome.Environments.Select(entry => entry.Name))}.");
        }

        if (request.CurrentEnvironment is not null)
        {
            document.Set([.. Project, "CurrentWorkspaceName"], request.CurrentEnvironment);
            outcome.Changes.Add($"Set the current environment to {request.CurrentEnvironment}.");
        }

        string[] apiKeyPath = [.. Settings, "ApiKey"];
        string[] apiSecretPath = [.. Settings, "ApiSecret"];
        bool hasApiKey = string.IsNullOrEmpty(document.GetString(apiKeyPath)) is false;
        if (hasApiKey && request.RotateSecret)
        {
            document.Remove(apiKeyPath);
            outcome.GeneratedApiSecret = _generateSecret();
            document.Set(apiSecretPath, outcome.GeneratedApiSecret);
            outcome.Changes.Add("Replaced the ApiKey with a new ApiSecret.");
        }
        else if (hasApiKey)
        {
            outcome.Warnings.Add("An ApiKey is configured, which overrides any ApiSecret; run with --rotate-secret to replace it with an ApiSecret.");
        }
        else if (ApiSecret.IsValid(document.GetString(apiSecretPath)) && request.RotateSecret is false)
        {
            outcome.Notes.Add("Kept the existing ApiSecret.");
        }
        else
        {
            outcome.GeneratedApiSecret = _generateSecret();
            document.Set(apiSecretPath, outcome.GeneratedApiSecret);
            outcome.Changes.Add("Generated a new ApiSecret.");
        }

        ApplyBaseline(document, request.Baseline, outcome);

        return outcome;
    }

    /// <summary>
    /// Gets the environment variable that sets a child secret, as Umbraco Deploy recommends instead of app settings files.
    /// </summary>
    public static string ChildSecretVariable(string name) => $"Umbraco__Deploy__Settings__ChildApiSecrets__{name}";

    private static int CountChildSecrets(JsonDocumentEditor document)
        => document.GetNode([.. Settings, "ChildApiSecrets"]) is JsonObject secrets ? secrets.Count : 0;

    private static string? FindChildSecretName(JsonDocumentEditor document, string name)
        => document.GetNode([.. Settings, "ChildApiSecrets"]) is JsonObject secrets
            ? secrets.Select(property => property.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            : null;

    private void ApplyBaseline(JsonDocumentEditor document, BaselineOptions? options, InitOutcome outcome)
    {
        string[] urlPath = [.. Settings, "BaselineUrl"];
        string[] secretPath = [.. Settings, "BaselineApiSecret"];
        string[] childSecretsPath = [.. Settings, "ChildApiSecrets"];

        if (options is { Remove: true })
        {
            bool removedUrl = document.Remove(urlPath);
            bool removedSecret = document.Remove(secretPath);
            if (removedUrl || removedSecret)
            {
                outcome.Changes.Add("Removed the Baseline URL and Baseline secret.");
            }
            else
            {
                outcome.Notes.Add("No Baseline URL or Baseline secret was configured.");
            }
        }

        if (options?.Url is not null)
        {
            document.Set(urlPath, options.Url);
            outcome.Changes.Add($"Set the Baseline URL to {options.Url}.");
        }

        if (options?.Secret is not null)
        {
            document.Set(secretPath, options.Secret);
            outcome.Changes.Add("Set the Baseline secret.");
        }
        else if (options is { RotateSecret: true })
        {
            outcome.GeneratedBaselineSecret = _generateSecret();
            document.Set(secretPath, outcome.GeneratedBaselineSecret);
            outcome.Changes.Add("Generated a new Baseline secret.");
        }

        if (options?.RemoveChildSecretName is string removeName)
        {
            string? existingName = FindChildSecretName(document, removeName);
            if (existingName is not null && document.Remove([.. childSecretsPath, existingName]))
            {
                outcome.Changes.Add($"Removed the child secret '{existingName}'.");
            }
            else
            {
                outcome.Notes.Add($"There is no child secret '{removeName}'.");
            }
        }

        if (options?.ChildSecretName is string name)
        {
            string? existingName = FindChildSecretName(document, name);
            if (existingName is not null && ApiSecret.IsValid(document.GetString([.. childSecretsPath, existingName])))
            {
                outcome.Notes.Add($"Kept the existing child secret '{existingName}'. To rotate it, add '{existingName}-1', switch the Child project to it, then remove '{existingName}'.");
            }
            else
            {
                outcome.GeneratedChildSecret = _generateSecret();
                outcome.GeneratedChildSecretName = existingName ?? name;
                document.Set([.. childSecretsPath, existingName ?? name], outcome.GeneratedChildSecret);
                outcome.Changes.Add($"Generated a new child secret '{outcome.GeneratedChildSecretName}'.");
                outcome.Warnings.Add(
                    $"The child secret '{outcome.GeneratedChildSecretName}' is stored in {FileName}: don't commit it to a repository that Child projects are created from. "
                    + $"Set it as the environment variable {ChildSecretVariable(outcome.GeneratedChildSecretName)} on the Baseline project's Live environment instead.");
            }
        }

        string? baselineUrl = document.GetString(urlPath);
        outcome.BaselineUrl = string.IsNullOrEmpty(baselineUrl) ? null : baselineUrl;
        outcome.BaselineSecretConfigured = string.IsNullOrEmpty(document.GetString(secretPath)) is false;
        outcome.ChildSecretCount = CountChildSecrets(document);

        if (options is { IsEmpty: false } && outcome.BaselineSecretConfigured && string.IsNullOrEmpty(outcome.BaselineUrl))
        {
            outcome.Warnings.Add("BaselineApiSecret is only used for calls to the Baseline project, so it has no effect without a BaselineUrl.");
        }
    }
}
