using System.Reflection;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Writes the files of the Cloud scaffold (the layout Umbraco Cloud creates for new Cloud projects), embedded in the CLI.
/// </summary>
/// <remarks>
/// The files are embedded with a <c>.template</c> suffix, so they don't affect this repository; <c>Scaffold/{major}/</c> overrides a file for one major.
/// </remarks>
internal static class CloudScaffold
{
    /// <summary>
    /// The name Umbraco Cloud gives the Umbraco project, which the README uses throughout.
    /// </summary>
    public const string DefaultName = "UmbracoProject";

    private static readonly ScaffoldFile[] Files =
    [
        new("README.md.template", "README.md", InProject: false, GitDeploymentOnly: true),
        new("appsettings.Staging.json.template", "appsettings.Staging.json", InProject: true, GitDeploymentOnly: false),
        new("appsettings.Production.json.template", "appsettings.Production.json", InProject: true, GitDeploymentOnly: false),
    ];

    /// <summary>
    /// Writes the scaffold files (overwriting existing ones) and returns their full paths; the README, which describes the
    /// git-based workflow, is only written for Git deployments.
    /// </summary>
    public static IReadOnlyList<string> Write(string repositoryRoot, string projectDirectory, string name, int major, bool gitDeployment)
    {
        var written = new List<string>();
        foreach (ScaffoldFile file in GetFiles(gitDeployment))
        {
            string content = Read(file.Resource, major);
            if (file.Target == "README.md" && name != DefaultName)
            {
                content = content.Replace(DefaultName, name, StringComparison.Ordinal);
            }

            string path = Path.Combine(file.InProject ? projectDirectory : repositoryRoot, file.Target);
            AtomicFile.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(content));
            written.Add(path);
        }

        return written;
    }

    /// <summary>
    /// Gets the root-level files of the scaffold (for the Solution Items of the solution).
    /// </summary>
    public static IEnumerable<string> GetRootFileNames(bool gitDeployment)
        => GetFiles(gitDeployment).Where(file => file.InProject is false).Select(file => file.Target);

    internal static string Read(string resource, int major)
    {
        Assembly assembly = typeof(CloudScaffold).Assembly;
        string[] names = assembly.GetManifestResourceNames();

        // RecursiveDir uses the platform's directory separator at build time
        string? name = names.FirstOrDefault(candidate => Normalize(candidate) == $"Scaffold/{major}/{resource}")
            ?? names.FirstOrDefault(candidate => Normalize(candidate) == $"Scaffold/{resource}")
            ?? throw new InvalidOperationException($"The embedded Cloud scaffold file '{resource}' is missing.");

        using Stream stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Normalize(string resourceName) => resourceName.Replace('\\', '/');

    private static IEnumerable<ScaffoldFile> GetFiles(bool gitDeployment) => Files.Where(file => gitDeployment || file.GitDeploymentOnly is false);

    private sealed record ScaffoldFile(string Resource, string Target, bool InProject, bool GitDeploymentOnly);
}
