namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// An Environment as configured for Umbraco Deploy (an item of the <c>Workspaces</c> configuration).
/// </summary>
internal sealed record WorkspaceEntry(Guid Id, string Name, string Type, string Url);
