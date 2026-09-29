using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// Configures Umbraco Deploy Cloud in <c>umbraco-cloud.json</c> (section <c>Deploy</c>).
/// </summary>
/// <remarks>
/// An ApiSecret is only written when no ApiKey is set, because Umbraco Cloud git-based projects still use the ApiKey (see ADR 0002).
/// </remarks>
internal sealed class CloudConfigurationWriter : IDeployConfigurationWriter
{
    private static readonly string[] Settings = ["Deploy", "Settings"];
    private static readonly string[] Project = ["Deploy", "Project"];

    private readonly Func<string> _generateSecret;
    private readonly Func<Guid> _newId;

    public CloudConfigurationWriter(Func<string> generateSecret, Func<Guid> newId)
    {
        _generateSecret = generateSecret;
        _newId = newId;
    }

    public DeployPackage Package => DeployPackage.Cloud;

    public string FileName => "umbraco-cloud.json";

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

        if (request.ProjectId is Guid projectId)
        {
            string[] projectIdPath = [.. Project, "Id"];
            string? existingProjectId = document.GetString(projectIdPath);
            if (string.IsNullOrEmpty(existingProjectId))
            {
                document.Set(projectIdPath, projectId.ToString());
                outcome.Changes.Add($"Set the project ID to {projectId}.");
            }
            else if (Guid.TryParse(existingProjectId, out Guid existingId) is false || existingId != projectId)
            {
                outcome.Warnings.Add($"The project ID is already set to {existingProjectId}, so it was kept.");
            }
        }

        string[] apiSecretPath = [.. Settings, "ApiSecret"];
        if (string.IsNullOrEmpty(document.GetString([.. Settings, "ApiKey"])) is false)
        {
            outcome.Notes.Add("No ApiSecret was written, because an ApiKey is configured and Umbraco Cloud git-based projects still use it.");
            if (request.RotateSecret)
            {
                outcome.Warnings.Add("--rotate-secret has no effect while an ApiKey is configured in umbraco-cloud.json.");
            }
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

        if (outcome.Changes.Count > 0)
        {
            outcome.Notes.Add("umbraco-cloud.json only reaches Umbraco Cloud through a deployment with --skip-preserve-umbraco-cloud-json.");
        }

        return outcome;
    }
}
