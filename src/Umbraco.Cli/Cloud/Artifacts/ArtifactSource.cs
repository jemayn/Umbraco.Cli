namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// What to upload: an existing zip file or a directory to zip (exactly one of <see cref="File" /> and <see cref="Directory" /> is set).
/// </summary>
internal sealed record ArtifactSource(string? File, string? Directory, DeploymentType? Type, string? Description, string? Version);
