using System.Text.Json.Nodes;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// Configures Umbraco Deploy Cloud in <c>umbraco-cloud.json</c> (section <c>Deploy</c>).
/// </summary>
/// <remarks>
/// Never writes an ApiSecret or ApiKey: Umbraco Cloud provisions them (see ADR 0008).
/// </remarks>
internal sealed class CloudConfigurationWriter : IDeployConfigurationWriter
{
    private static readonly string[] Settings = ["Deploy", "Settings"];
    private static readonly string[] Project = ["Deploy", "Project"];

    private readonly Func<Guid> _newId;

    public CloudConfigurationWriter(Func<Guid> newId) => _newId = newId;

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

        if (string.IsNullOrEmpty(document.GetString([.. Settings, "ApiSecret"])) && string.IsNullOrEmpty(document.GetString([.. Settings, "ApiKey"])))
        {
            outcome.Notes.Add("No ApiSecret is configured in umbraco-cloud.json; Umbraco Cloud provisions it for its environments, so the CLI doesn't generate one.");
        }

        ReportBaseline(document, outcome);

        if (outcome.Changes.Count > 0)
        {
            outcome.Notes.Add("umbraco-cloud.json only reaches Umbraco Cloud through a deployment with --skip-preserve-umbraco-cloud-json.");
        }

        return outcome;
    }

    private static void ReportBaseline(JsonDocumentEditor document, InitOutcome outcome)
    {
        string? baselineUrl = document.GetString([.. Settings, "BaselineUrl"]);
        if (string.IsNullOrEmpty(baselineUrl))
        {
            baselineUrl = document.GetString([.. Project, "Upstream"]);
        }

        outcome.BaselineUrl = string.IsNullOrEmpty(baselineUrl) ? null : baselineUrl;
        outcome.BaselineSecretConfigured = string.IsNullOrEmpty(document.GetString([.. Settings, "BaselineApiSecret"])) is false;
        outcome.ChildSecretCount = document.GetNode([.. Settings, "ChildApiSecrets"]) is JsonObject childSecrets ? childSecrets.Count : 0;

        if (outcome.BaselineUrl is null && outcome.BaselineSecretConfigured is false && outcome.ChildSecretCount == 0)
        {
            return;
        }

        if (outcome.BaselineUrl is not null)
        {
            outcome.Notes.Add($"The Baseline URL is {outcome.BaselineUrl}.");
        }

        if (outcome.BaselineSecretConfigured)
        {
            outcome.Notes.Add("A Baseline secret is configured.");
        }

        if (outcome.ChildSecretCount > 0)
        {
            outcome.Notes.Add($"{outcome.ChildSecretCount} child secret{(outcome.ChildSecretCount == 1 ? string.Empty : "s")} configured.");
        }

        outcome.Notes.Add("The ApiSecret is per project, and Umbraco Cloud provisions the Baseline settings, so they were not changed.");
    }
}
