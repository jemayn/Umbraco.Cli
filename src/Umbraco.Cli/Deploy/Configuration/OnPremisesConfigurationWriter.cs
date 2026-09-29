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

        return outcome;
    }
}
