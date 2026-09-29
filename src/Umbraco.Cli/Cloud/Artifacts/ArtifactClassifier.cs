namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// Detects the deployment type of an Artifact with the same rules as Umbraco Cloud.
/// </summary>
internal static class ArtifactClassifier
{
    private static readonly string[] ProjectExtensions = [".csproj", ".sln", ".slnx", ".fsproj", ".vbproj"];

    /// <summary>
    /// Classifies an Artifact from the relative paths of its files.
    /// </summary>
    /// <returns>
    /// <see cref="DeploymentType.Artifact" /> if <c>Umbraco.Core.dll</c> is at the root, otherwise <see cref="DeploymentType.Git" /> if a project or solution file exists at any depth, otherwise <c>null</c>.
    /// </returns>
    public static DeploymentType? Classify(IEnumerable<string> relativePaths)
    {
        bool hasProjectFile = false;
        foreach (string path in relativePaths)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized[2..];
            }

            if (string.Equals(normalized, "Umbraco.Core.dll", StringComparison.OrdinalIgnoreCase))
            {
                return DeploymentType.Artifact;
            }

            if (ProjectExtensions.Any(extension => normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                hasProjectFile = true;
            }
        }

        return hasProjectFile ? DeploymentType.Git : null;
    }
}
