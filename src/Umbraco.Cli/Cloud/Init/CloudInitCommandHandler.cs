using System.Text;
using NuGet.Versioning;
using Umbraco.Cli.Deploy.ProjectDetection;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Releases;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// <c>umbraco cloud init</c>: prepares an Umbraco project for Umbraco Cloud, creating it first when asked.
/// </summary>
internal sealed class CloudInitCommandHandler
{
    private const string CoreCmsPackageId = "Umbraco.Cms.Core";

    private readonly IEnvironment _environment;
    private readonly IDotnetCli _dotnetCli;
    private readonly UmbracoReleases _releases;
    private readonly PackageVersionResolver _versionResolver;
    private readonly UmbracoProjectCreator _creator;
    private readonly IPrompter _prompter;
    private readonly TimeProvider _timeProvider;
    private readonly IOutput _output;
    private readonly IReporter _reporter;

    public CloudInitCommandHandler(
        IEnvironment environment,
        IDotnetCli dotnetCli,
        UmbracoReleases releases,
        PackageVersionResolver versionResolver,
        UmbracoProjectCreator creator,
        IPrompter prompter,
        TimeProvider timeProvider,
        IOutput output,
        IReporter reporter)
    {
        _environment = environment;
        _dotnetCli = dotnetCli;
        _releases = releases;
        _versionResolver = versionResolver;
        _creator = creator;
        _prompter = prompter;
        _timeProvider = timeProvider;
        _output = output;
        _reporter = reporter;
    }

    public async Task<int> HandleAsync(CloudInitSettings settings, CancellationToken cancellationToken)
    {
        bool interactive = settings.NonInteractive is false && _environment.IsInputRedirected is false && _environment.IsErrorRedirected is false;
        ProductCatalog catalog = ProductCatalog.LoadEmbedded();
        ValidateProductKeys(settings.Add, catalog);
        IdentityValues? identity = IdentityValues.Resolve(settings.IdentityClientId, settings.IdentityClientSecret, _environment);

        string? name = settings.Name;
        if (name is null && settings.Project is null && HasProjectFile(_environment.CurrentDirectory) is false)
        {
            name = interactive
                ? await _prompter.AskAsync("There is no Umbraco project here. Name of the new Umbraco project:", CloudScaffold.DefaultName, cancellationToken)
                : throw new UsageException(
                    $"No project file was found in '{_environment.CurrentDirectory}'.",
                    "Use --name to create a new Umbraco project, or --project to prepare an existing one.");
        }

        bool create = name is not null;
        if (create is false && (settings.Version is not null || settings.Output is not null || settings.DevelopmentMode is not null || settings.ExcludeGitignore || settings.AllowEndOfLife || settings.TemplateOptions.Count > 0))
        {
            throw new UsageException("--version, --output, --development-mode, --exclude-gitignore, --allow-end-of-life and template options only apply when creating a new Umbraco project with --name.");
        }

        if (create && settings.Project is not null)
        {
            throw new UsageException("Use either --name (to create a new Umbraco project) or --project (to prepare an existing one), not both.");
        }

        var filesWritten = new List<string>();
        string projectPath;
        NuGetVersion cmsVersion;
        IReadOnlyDictionary<string, string> referenced;
        string repositoryRoot;
        if (create)
        {
            repositoryRoot = UmbracoProjectCreator.ValidateTarget(name!, settings.Output, settings.ExcludeGitignore, settings.IsGitDeployment, _environment.CurrentDirectory);
            cmsVersion = await ResolveNewVersionAsync(settings, interactive, cancellationToken);
            projectPath = await _creator.CreateAsync(name!, repositoryRoot, cmsVersion, settings.DevelopmentMode ?? "ide", settings.ExcludeGitignore, settings.IsGitDeployment, settings.TemplateOptions, catalog, filesWritten, cancellationToken);
            referenced = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            projectPath = ProjectLocator.Locate(settings.Project, _environment.CurrentDirectory);
            string sdkVersion = await _dotnetCli.GetSdkVersionAsync(Path.GetDirectoryName(projectPath)!, cancellationToken);
            DeployPackageDetector.EnsureSupportedSdk(sdkVersion);

            PackageList packages = PackageList.Parse(await _dotnetCli.ListPackagesAsync(projectPath, interactive, cancellationToken));
            cmsVersion = GetCmsVersion(packages, projectPath);
            referenced = packages.TopLevel;
            repositoryRoot = DotUmbracoFile.FindRoot(Path.GetDirectoryName(projectPath)!);
        }

        UmbracoMajor major = await _releases.GetMajorAsync(cmsVersion.Major, cancellationToken);
        if (create is false)
        {
            WarnAboutLifecycle(major);
        }

        IReadOnlyList<string> selected = settings.Add.Count > 0 || interactive is false
            ? settings.Add
            : await SelectProductsAsync(catalog, referenced, cancellationToken);

        PackagePlan plan = PackagePlan.Create(catalog, selected, referenced);
        IReadOnlyList<CloudInitPackage> packageResults = await InstallPackagesAsync(plan, projectPath, cmsVersion, cancellationToken);

        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        CloudProjectSettings.EnsureLicenses(
            projectDirectory,
            plan.PresentProducts.Select(product => product.LicenseKey).OfType<string>(),
            filesWritten);
        string identityState = CloudProjectSettings.EnsureUmbracoCloudJson(projectDirectory, identity, filesWritten);
        if (create is false && settings.IsGitDeployment && DotUmbracoFile.Ensure(projectPath, message => _reporter.Warning(message)) is string dotUmbraco)
        {
            filesWritten.Add(dotUmbraco);
        }

        if (create is false)
        {
            IReadOnlyList<string> missing = GitignoreHints.GetMissing(repositoryRoot);
            if (missing.Count > 0)
            {
                _reporter.Info($"hint: The Cloud scaffold's .gitignore also ignores {string.Join(", ", missing)}; consider adding them to {Path.Combine(repositoryRoot, ".gitignore")}.");
            }
        }

        bool built = await RestoreAndBuildAsync(projectPath, settings.NoBuild, cancellationToken);
        var result = new CloudInitResult
        {
            Created = create,
            DeploymentType = settings.DeploymentType,
            RepositoryRoot = repositoryRoot,
            ProjectPath = projectPath,
            CmsVersion = cmsVersion.ToNormalizedString(),
            ReleaseType = major.ReleaseType == ReleaseType.Lts ? "LTS" : "STS",
            LifecyclePhase = FormatPhase(major.GetPhase(_timeProvider.GetUtcNow())),
            Packages = packageResults,
            FilesWritten = filesWritten.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Identity = identityState,
            Built = built,
        };

        _output.Write(result, CliJsonContext.Default.CloudInitResult, _ => FormatText(result, major));
        return ExitCodes.Success;
    }

    private static void ValidateProductKeys(IReadOnlyList<string> keys, ProductCatalog catalog)
    {
        foreach (string key in keys)
        {
            ProductCatalog.Product? product = catalog.FindProduct(key);
            if (product is null || product.Required)
            {
                string available = string.Join(", ", catalog.Products.Where(candidate => candidate.Required is false).Select(candidate => candidate.Key));
                throw new UsageException($"'{key}' is not a Product that can be added.", $"Use --add with one of: {available}.");
            }
        }
    }

    private static bool HasProjectFile(string directory) => Directory.EnumerateFiles(directory, "*.csproj").Any();

    private static NuGetVersion GetCmsVersion(PackageList packages, string projectPath)
    {
        if (packages.All.TryGetValue(CoreCmsPackageId, out string? version) is false || NuGetVersion.TryParse(version, out NuGetVersion? cmsVersion) is false)
        {
            throw new UsageException($"'{Path.GetFileName(projectPath)}' is not an Umbraco project: it doesn't reference {CoreCmsPackageId}.");
        }

        if (cmsVersion.Major < UmbracoMajor.FirstSupportedMajor)
        {
            throw new UsageException(
                $"'{Path.GetFileName(projectPath)}' uses Umbraco {cmsVersion}, but umbraco cloud init supports Umbraco {UmbracoMajor.FirstSupportedMajor} and later.",
                $"Upgrade the Umbraco project to Umbraco {UmbracoMajor.FirstSupportedMajor} or later first.");
        }

        return cmsVersion;
    }

    private static string FormatPhase(LifecyclePhase phase) => phase == LifecyclePhase.EndOfLife ? "end-of-life" : phase.ToString().ToLowerInvariant();

    private static string FormatText(CloudInitResult result, UmbracoMajor major)
    {
        var text = new StringBuilder();
        text.AppendLine(result.Created
            ? $"Created the Umbraco project {result.ProjectPath} (Umbraco {result.CmsVersion}) for Umbraco Cloud."
            : $"Prepared {result.ProjectPath} (Umbraco {result.CmsVersion}) for Umbraco Cloud.");

        foreach (CloudInitPackage package in result.Packages)
        {
            string reason = package.Reason == PlannedPackage.IntegrationReason ? "Integration package" : package.Reason;
            text.AppendLine(package.Action switch
            {
                "added" => $"  Added {package.Id} {package.Version} ({reason}).",
                "referenced" => $"  Kept {package.Id} {package.Version} (already referenced).",
                _ => $"  Skipped {package.Id}: no version is compatible with Umbraco {result.CmsVersion}.",
            });
        }

        foreach (string file in result.FilesWritten)
        {
            text.AppendLine($"  Wrote {file}");
        }

        if (result.Identity == "placeholder")
        {
            text.AppendLine($"  umbraco-cloud.json has placeholder Umbraco ID values for local login: the backoffice login works locally; for local Umbraco ID login, set {IdentityValues.ClientIdVariable} and {IdentityValues.ClientSecretVariable} to the values in a cloned Cloud project's umbraco-cloud.json and run umbraco cloud init again (Umbraco Cloud environments use their own values).");
        }

        text.AppendLine(result.Built ? "  Restored and built the project." : "  Restored the project.");
        text.AppendLine();
        text.AppendLine("Next steps:");
        text.AppendLine("  1. Configure Umbraco Deploy (an ApiSecret shared by all environments, and optionally the environments to transfer content between):");
        text.AppendLine($"       umbraco deploy init --project \"{result.ProjectPath}\"");
        text.AppendLine("  2. Run the project locally first, so Umbraco installs and Umbraco Deploy writes the schema to umbraco/Deploy/Revision:");
        text.AppendLine($"       dotnet run --project \"{result.ProjectPath}\"");
        if (result.DeploymentType == "git")
        {
            text.AppendLine("  3. Deploy the source code to Umbraco Cloud (a Git deployment, which needs a git work tree):");
            text.AppendLine($"       umbraco cloud deployments deploy --path \"{result.RepositoryRoot}\" --environment <alias>");
        }
        else
        {
            text.AppendLine("  3. Deploy the published output to Umbraco Cloud (an Artifact deployment):");
            text.AppendLine($"       dotnet publish \"{result.ProjectPath}\" --configuration Release --output publish");
            text.AppendLine("       umbraco cloud deployments deploy --path publish --environment <alias>");
        }
        return text.ToString().TrimEnd();
    }

    private async Task<NuGetVersion> ResolveNewVersionAsync(CloudInitSettings settings, bool interactive, CancellationToken cancellationToken)
    {
        IReadOnlyList<NuGetVersion> versions = await _releases.GetTemplateVersionsAsync(cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        NuGetVersion version;
        if (settings.Version is null && interactive)
        {
            var choices = new List<(string Label, NuGetVersion Version)>();
            foreach (IGrouping<int, NuGetVersion> group in versions.Where(candidate => candidate.IsPrerelease is false).GroupBy(candidate => candidate.Major).OrderByDescending(group => group.Key))
            {
                UmbracoMajor candidateMajor = await _releases.GetMajorAsync(group.Key, cancellationToken);
                if (candidateMajor.GetPhase(now) != LifecyclePhase.EndOfLife)
                {
                    NuGetVersion newest = group.Max()!;
                    choices.Add(($"Umbraco {newest} ({candidateMajor.Describe(now)})", newest));
                }
            }

            if (choices.Count == 0)
            {
                throw new CliException("No supported Umbraco version was found on NuGet.");
            }

            string selected = await _prompter.SelectAsync("Which Umbraco version?", choices.Select(choice => choice.Label).ToList(), cancellationToken);
            version = choices.First(choice => choice.Label == selected).Version;
        }
        else
        {
            version = UmbracoReleases.Resolve(settings.Version ?? "latest", versions);
        }

        UmbracoMajor major = await _releases.GetMajorAsync(version.Major, cancellationToken);
        LifecyclePhase phase = major.GetPhase(now);
        if (phase == LifecyclePhase.EndOfLife && settings.AllowEndOfLife is false)
        {
            throw new UsageException(
                $"Umbraco {version.Major} is end-of-life ({major.Describe(now)}).",
                "Choose a supported version, or use --allow-end-of-life.");
        }

        WarnAboutLifecycle(major);
        NuGetVersion? newestStable = versions.Where(candidate => candidate.IsPrerelease is false && candidate.Major == version.Major).Max();
        if (version.IsPrerelease && newestStable is not null && newestStable > version)
        {
            _reporter.Warning($"Umbraco {version} is a prerelease, but {newestStable} is already released.");
        }

        return version;
    }

    private void WarnAboutLifecycle(UmbracoMajor major)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        switch (major.GetPhase(now))
        {
            case LifecyclePhase.Security:
                _reporter.Warning($"Umbraco {major.Major} only receives security fixes ({major.Describe(now)}).", "Consider upgrading to a newer or LTS version.");
                break;

            case LifecyclePhase.EndOfLife:
                _reporter.Warning($"Umbraco {major.Major} is end-of-life ({major.Describe(now)}).", "Upgrade to a supported version.");
                break;
        }
    }

    private async Task<IReadOnlyList<string>> SelectProductsAsync(ProductCatalog catalog, IReadOnlyDictionary<string, string> referenced, CancellationToken cancellationToken)
    {
        List<ProductCatalog.Product> optional = catalog.Products
            .Where(product => product.Required is false && product.Packages.All(referenced.ContainsKey) is false)
            .ToList();

        if (optional.Count == 0)
        {
            return [];
        }

        IReadOnlyList<string> labels = optional.Select(product => $"{product.Name} ({product.Key})").ToList();
        IReadOnlyList<string> selected = await _prompter.SelectManyAsync("Which Umbraco Products should be added?", labels, cancellationToken);
        return optional.Where(product => selected.Contains($"{product.Name} ({product.Key})")).Select(product => product.Key).ToList();
    }

    private async Task<IReadOnlyList<CloudInitPackage>> InstallPackagesAsync(PackagePlan plan, string projectPath, NuGetVersion cmsVersion, CancellationToken cancellationToken)
    {
        var results = new List<CloudInitPackage>();
        foreach (PlannedPackage package in plan.Packages)
        {
            if (package.IsReferenced)
            {
                if (NuGetVersion.TryParse(package.ExistingVersion, out NuGetVersion? existing) && existing.Major != cmsVersion.Major)
                {
                    _reporter.Warning($"{package.Id} {existing} doesn't match Umbraco {cmsVersion.Major}; Umbraco Cloud requires the same major.");
                }

                results.Add(new CloudInitPackage(package.Id, package.ExistingVersion, "referenced", package.Reason));
                continue;
            }

            NuGetVersion? version = await _versionResolver.ResolveAsync(package.Id, cmsVersion, cancellationToken);
            if (version is null)
            {
                if (package.IsIntegration)
                {
                    _reporter.Warning($"No version of {package.Id} is compatible with Umbraco {cmsVersion}, so it was skipped.");
                    results.Add(new CloudInitPackage(package.Id, null, "unavailable", package.Reason));
                    continue;
                }

                throw new UsageException($"No version of {package.Id} is compatible with Umbraco {cmsVersion}.");
            }

            _reporter.Info($"Adding {package.Id} {version}.");
            await _dotnetCli.RunAsync(
                ["package", "add", package.Id, "--version", version.ToNormalizedString(), "--project", projectPath, "--no-restore"],
                Path.GetDirectoryName(projectPath)!,
                cancellationToken);

            results.Add(new CloudInitPackage(package.Id, version.ToNormalizedString(), "added", package.Reason));
        }

        return results;
    }

    private async Task<bool> RestoreAndBuildAsync(string projectPath, bool noBuild, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(projectPath)!;
        _reporter.Info("Restoring the project.");
        await _dotnetCli.RunAsync(["restore", projectPath], directory, cancellationToken);
        if (noBuild)
        {
            return false;
        }

        _reporter.Info("Building the project.");
        await _dotnetCli.RunAsync(["build", projectPath, "--no-restore"], directory, cancellationToken);
        return true;
    }
}
