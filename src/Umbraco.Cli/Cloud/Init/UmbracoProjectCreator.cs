using System.Text.RegularExpressions;
using NuGet.Versioning;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Releases;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Creates a new Umbraco project with the Umbraco.Templates <c>umbraco</c> template, laid out like the Cloud scaffold.
/// </summary>
internal sealed partial class UmbracoProjectCreator
{
    /// <summary>
    /// The options the Cloud scaffold passes to the <c>umbraco</c> template.
    /// </summary>
    private static readonly string[] CloudTemplateOptions =
    [
        "--use-https-redirect",
        "--no-nodes-view-path", "~/umbraco/UmbracoWebsite/Deploy/NoNodes.cshtml",
        "--package-management", "Central",
    ];

    /// <summary>
    /// The Umbraco Deploy rules added to the template's minimal (Umbraco-specific) <c>.gitignore</c>.
    /// </summary>
    internal const string DeployGitignoreRules = "\n# Umbraco Deploy trigger and status files (the schema files in umbraco/Deploy/Revision are committed)\n/umbraco/Deploy/deploy*\n";

    private readonly IDotnetCli _dotnetCli;
    private readonly IEnvironment _environment;
    private readonly IReporter _reporter;

    public UmbracoProjectCreator(IDotnetCli dotnetCli, IEnvironment environment, IReporter reporter)
    {
        _dotnetCli = dotnetCli;
        _environment = environment;
        _reporter = reporter;
    }

    /// <summary>
    /// Validates the project name and the output directory before anything is created.
    /// </summary>
    /// <returns>The full path of the repository root.</returns>
    public static string ValidateTarget(string name, string? output, bool excludeGitignore, bool gitDeployment, string currentDirectory)
    {
        if (NamePattern().IsMatch(name) is false)
        {
            throw new UsageException($"'{name}' is not a valid project name.", "Use letters, digits, '.', '_' or '-', starting with a letter.");
        }

        string root = Path.GetFullPath(output ?? currentDirectory, currentDirectory);

        // Other files (such as a README, LICENSE or local package feed) may exist, as long as nothing would be overwritten
        List<string> conflicts = GetCreatedPaths(name, excludeGitignore, gitDeployment)
            .Where(path => File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path)))
            .ToList();
        if (conflicts.Count > 0)
        {
            throw new UsageException(
                $"'{root}' already contains {string.Join(", ", conflicts)}.",
                "Create the Umbraco project in a directory without these files, or use --output.");
        }

        return root;
    }

    /// <summary>
    /// Gets the paths (relative to the repository root) that creating an Umbraco project writes.
    /// </summary>
    internal static IEnumerable<string> GetCreatedPaths(string name, bool excludeGitignore, bool gitDeployment)
        => [Path.Combine("src", name), .. GetRootFiles(name, excludeGitignore, gitDeployment)];

    /// <summary>
    /// Gets the files created at the repository root (also listed as Solution Items, apart from the solution itself).
    /// </summary>
    private static IEnumerable<string> GetRootFiles(string name, bool excludeGitignore, bool gitDeployment)
        => [
            .. gitDeployment ? [DotUmbracoFile.FileName] : Array.Empty<string>(),
            $"{name}.slnx",
            "Directory.Packages.props",
            .. CloudScaffold.GetRootFileNames(gitDeployment),
            .. excludeGitignore ? Array.Empty<string>() : [".gitignore"],
        ];

    /// <summary>
    /// Creates the Umbraco project, the Cloud scaffold files and the solution.
    /// </summary>
    /// <returns>The full path of the project file.</returns>
    public async Task<string> CreateAsync(
        string name,
        string repositoryRoot,
        NuGetVersion templateVersion,
        string developmentMode,
        bool excludeGitignore,
        bool gitDeployment,
        IReadOnlyList<string> templateOptions,
        ProductCatalog catalog,
        List<string> filesWritten,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(repositoryRoot);
        string hive = await EnsureTemplateAsync(templateVersion, cancellationToken);

        string relativeProjectDirectory = Path.Combine("src", name);
        string projectDirectory = Path.Combine(repositoryRoot, relativeProjectDirectory);
        _reporter.Info($"Creating the Umbraco {templateVersion} project {name} in {projectDirectory}.");
        // A development mode given in the template options wins over the --development-mode option
        string[] developmentModeOptions = templateOptions.Any(option => option is "-dm" or "--development-mode")
            ? []
            : ["--development-mode", developmentMode == "backoffice" ? "BackofficeDevelopment" : "IDEDevelopment"];
        await _dotnetCli.RunAsync(
            [
                "new", "umbraco", "-n", name, "-o", projectDirectory, .. CloudTemplateOptions, .. developmentModeOptions,
                excludeGitignore ? "--exclude-gitignore" : "--minimal-gitignore",
                "--no-restore", "--no-update-check", "--debug:custom-hive", hive, .. templateOptions,
            ],
            repositoryRoot,
            cancellationToken);

        string projectPath = Path.Combine(projectDirectory, $"{name}.csproj");
        if (File.Exists(projectPath) is false)
        {
            throw new CliException($"The umbraco template didn't create {projectPath}.");
        }

        // The Cloud scaffold keeps central package management at the repository root
        string packagesProps = Path.Combine(projectDirectory, "Directory.Packages.props");
        if (File.Exists(packagesProps))
        {
            string rootPackagesProps = Path.Combine(repositoryRoot, "Directory.Packages.props");
            File.Move(packagesProps, rootPackagesProps, overwrite: true);
            filesWritten.Add(rootPackagesProps);
        }

        filesWritten.AddRange(CloudScaffold.Write(repositoryRoot, projectDirectory, name, templateVersion.Major, gitDeployment));
        if (excludeGitignore is false)
        {
            filesWritten.AddRange(await CreateGitignoreAsync(repositoryRoot, projectDirectory, cancellationToken));
        }

        if (catalog.Scaffold.VerifiedMajors.Contains(templateVersion.Major) is false)
        {
            _reporter.Warning($"The Cloud scaffold wasn't verified for Umbraco {templateVersion.Major}; compare it with a new Cloud project of that version.");
        }

        // Umbraco Cloud builds a Git deployment from the project .umbraco points at (an Artifact deployment is already built)
        if (gitDeployment)
        {
            string dotUmbraco = Path.Combine(repositoryRoot, DotUmbracoFile.FileName);
            AtomicFile.WriteAllBytes(dotUmbraco, System.Text.Encoding.UTF8.GetBytes(DotUmbracoFile.Create(relativeProjectDirectory, $"{name}.csproj")));
            filesWritten.Add(dotUmbraco);
        }

        string solution = await CreateSolutionAsync(name, repositoryRoot, projectPath, excludeGitignore, gitDeployment, cancellationToken);
        filesWritten.Add(solution);
        return projectPath;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]*$")]
    private static partial Regex NamePattern();

    /// <summary>
    /// Installs the Umbraco.Templates version into its own template store, so the user's installed templates are never changed.
    /// </summary>
    private async Task<string> EnsureTemplateAsync(NuGetVersion version, CancellationToken cancellationToken)
    {
        string hive = Path.Combine(_environment.CacheDirectory, "templates", version.ToNormalizedString());
        string marker = Path.Combine(hive, ".installed");
        if (File.Exists(marker))
        {
            _reporter.Verbose($"Using the cached Umbraco.Templates {version} in {hive}.");
            return hive;
        }

        _reporter.Info($"Installing Umbraco.Templates {version}.");
        Directory.CreateDirectory(hive);
        await _dotnetCli.RunAsync(
            ["new", "install", $"{UmbracoReleases.TemplatesPackageId}@{version.ToNormalizedString()}", "--debug:custom-hive", hive],
            hive,
            cancellationToken);

        await File.WriteAllTextAsync(marker, version.ToNormalizedString(), cancellationToken);
        return hive;
    }

    /// <summary>
    /// Creates the .NET <c>.gitignore</c> at the repository root and adds the Umbraco Deploy rules to the template's Umbraco-specific one.
    /// </summary>
    private async Task<IReadOnlyList<string>> CreateGitignoreAsync(string repositoryRoot, string projectDirectory, CancellationToken cancellationToken)
    {
        await _dotnetCli.RunAsync(["new", "gitignore", "-o", repositoryRoot], repositoryRoot, cancellationToken);

        string projectGitignore = Path.Combine(projectDirectory, ".gitignore");
        await File.AppendAllTextAsync(projectGitignore, DeployGitignoreRules, cancellationToken);
        return [Path.Combine(repositoryRoot, ".gitignore"), projectGitignore];
    }

    private async Task<string> CreateSolutionAsync(string name, string repositoryRoot, string projectPath, bool excludeGitignore, bool gitDeployment, CancellationToken cancellationToken)
    {
        await _dotnetCli.RunAsync(["new", "sln", "-n", name, "-o", repositoryRoot, "--format", "slnx"], repositoryRoot, cancellationToken);

        string solution = Path.Combine(repositoryRoot, $"{name}.slnx");
        await _dotnetCli.RunAsync(["sln", solution, "add", projectPath], repositoryRoot, cancellationToken);

        SolutionFile.AddSolutionItems(solution, GetRootFiles(name, excludeGitignore, gitDeployment).Where(file => file != $"{name}.slnx"));
        return solution;
    }
}
