namespace Umbraco.Cli.Deploy.ProjectDetection;

/// <summary>
/// The Umbraco Deploy package found in a project.
/// </summary>
internal sealed record DetectedPackage(DeployPackage Package, string PackageId, string Version);
