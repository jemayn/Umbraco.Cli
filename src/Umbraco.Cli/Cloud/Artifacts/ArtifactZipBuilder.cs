using System.IO.Compression;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Cloud.Artifacts;

/// <summary>
/// Creates the zip file to upload from a directory.
/// </summary>
internal sealed class ArtifactZipBuilder
{
    public const string DotUmbracoFileName = ".umbraco";

    private readonly IGit _git;

    public ArtifactZipBuilder(IGit git) => _git = git;

    /// <summary>
    /// Zips a directory: all files for an Artifact deployment, or the files git would commit (never <c>.git</c>, always <c>.gitignore</c>) for a Git deployment,
    /// leaving out the <see cref="ArtifactExclusions" />. When <c>type</c> is <c>null</c>, it is detected like Umbraco Cloud does.
    /// </summary>
    public async Task<ArtifactPackage> CreateAsync(string directory, DeploymentType? type, CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) is false)
        {
            throw new UsageException($"The directory '{directory}' does not exist.");
        }

        IReadOnlyList<string> allFiles = EnumerateFiles(root);
        DeploymentType resolvedType = type ?? ArtifactClassifier.Classify(allFiles) ?? throw new UsageException(
            $"Could not detect the deployment type of '{directory}': it contains neither Umbraco.Core.dll at its root (published output) nor a project or solution file (source code).",
            "Use --type git or --type artifact.");

        IReadOnlyList<string> candidates = resolvedType == DeploymentType.Git ? await GetGitFilesAsync(root, cancellationToken) : allFiles;
        List<string> excluded = candidates.Where(ArtifactExclusions.IsExcluded).ToList();
        List<string> files = candidates.Where(file => ArtifactExclusions.IsExcluded(file) is false).ToList();

        string zipPath = Path.Combine(Path.GetTempPath(), $"umbraco-cli-{Guid.NewGuid():N}.zip");
        try
        {
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    zip.CreateEntryFromFile(Path.Combine(root, file), file, CompressionLevel.Optimal);
                }
            }
        }
        catch
        {
            File.Delete(zipPath);
            throw;
        }

        bool hasDotUmbraco = files.Contains(DotUmbracoFileName, StringComparer.Ordinal);
        return new ArtifactPackage(zipPath, resolvedType, hasDotUmbraco, isTemporary: true) { ExcludedFiles = excluded };
    }

    private static List<string> EnumerateFiles(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        return Directory.EnumerateFiles(root, "*", options)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => IsInGitDirectory(path) is false)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsRegularFile(string path)
    {
        var file = new FileInfo(path);
        return file.Exists && file.LinkTarget is null;
    }

    private static bool IsInGitDirectory(string relativePath)
        => relativePath.StartsWith(".git/", StringComparison.Ordinal) || relativePath.Contains("/.git/", StringComparison.Ordinal);

    private async Task<IReadOnlyList<string>> GetGitFilesAsync(string root, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> listed = await _git.ListFilesAsync(root, cancellationToken);

        // Files deleted from the work tree are still listed while they're in the index, and symbolic links are skipped
        // (like in an Artifact deployment), so a link can't add content from outside the directory
        var files = listed
            .Where(path => IsInGitDirectory(path) is false && path != ".git")
            .Where(path => IsRegularFile(Path.Combine(root, path)))
            .ToList();

        // Umbraco Cloud stages the zip honouring its own .gitignore, so it must always be included
        if (files.Contains(".gitignore", StringComparer.Ordinal) is false && File.Exists(Path.Combine(root, ".gitignore")))
        {
            files.Add(".gitignore");
        }

        return files;
    }
}
