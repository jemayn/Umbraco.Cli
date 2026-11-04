using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// What to upload: an existing zip file or a directory to zip (at most one of <see cref="File" /> and <see cref="Directory" /> is set).
/// </summary>
internal sealed record ArtifactSource(string? File, string? Directory, DeploymentType? Type, string? Description, string? Version)
{
    /// <summary>
    /// Gets this source, or when neither a file nor a directory is given, the source code of the repository containing <paramref name="currentDirectory" />
    /// when it has a <c>.umbraco</c> file at its root (as the repository of a Git deployment does).
    /// </summary>
    /// <exception cref="UsageException">There's no <c>.umbraco</c> file, so the published output (or another source) must be given.</exception>
    public ArtifactSource WithDefault(string currentDirectory)
    {
        if (File is not null || Directory is not null)
        {
            return this;
        }

        string? root = RepositoryRoot.Find(currentDirectory);
        if (root is not null && System.IO.File.Exists(Path.Combine(root, DotUmbracoFile.FileName)))
        {
            return this with { Directory = root };
        }

        throw new UsageException(
            "No artifact to upload was specified.",
            "Use --path with the published output for an Artifact deployment (for example after 'dotnet publish --output publish', use --path publish), "
            + $"--path with the source code for a Git deployment (the default in a repository with a {DotUmbracoFile.FileName} file), or --file with a zip file.");
    }
}
