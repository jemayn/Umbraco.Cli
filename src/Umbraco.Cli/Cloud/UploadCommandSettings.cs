using Umbraco.Cli.Cloud.Artifacts;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// The options of <c>umbraco cloud deployments upload</c>.
/// </summary>
internal sealed record UploadCommandSettings(string? ProjectId, string? ApiKey, ArtifactSource Source, string? Config = null);
