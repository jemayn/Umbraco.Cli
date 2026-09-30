using System.Globalization;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Deploy.ProjectDetection;

/// <summary>
/// Detects which Umbraco Deploy package a project references, using the .NET SDK to resolve package references.
/// </summary>
internal sealed class DeployPackageDetector
{
    public const string OnPremisesPackageId = "Umbraco.Deploy.OnPrem";

    public const string CloudPackageId = "Umbraco.Deploy.Cloud";

    public const int MinimumSdkMajorVersion = 10;

    private readonly IDotnetCli _dotnetCli;

    public DeployPackageDetector(IDotnetCli dotnetCli) => _dotnetCli = dotnetCli;

    public async Task<DetectedPackage> DetectAsync(string projectPath, bool interactive, CancellationToken cancellationToken)
    {
        string sdkVersion = await _dotnetCli.GetSdkVersionAsync(Path.GetDirectoryName(projectPath)!, cancellationToken);
        EnsureSupportedSdk(sdkVersion);

        string json = await _dotnetCli.ListPackagesAsync(projectPath, interactive, cancellationToken);
        return FindDeployPackage(json, projectPath);
    }

    internal static void EnsureSupportedSdk(string sdkVersion)
    {
        string major = sdkVersion.Split('.', '-')[0];
        if (int.TryParse(major, NumberStyles.None, CultureInfo.InvariantCulture, out int majorVersion) is false || majorVersion < MinimumSdkMajorVersion)
        {
            throw new CliException(
                $".NET SDK {MinimumSdkMajorVersion} or later is required (found {sdkVersion}, possibly pinned by global.json).",
                ExitCodes.Error,
                "Install the .NET SDK 10, or update the global.json that pins the SDK version.");
        }
    }

    internal static DetectedPackage FindDeployPackage(string packageListJson, string projectPath)
    {
        PackageList packages = PackageList.Parse(packageListJson);
        var found = new List<DetectedPackage>();
        if (packages.All.TryGetValue(OnPremisesPackageId, out string? onPremisesVersion))
        {
            found.Add(new DetectedPackage(DeployPackage.OnPremises, OnPremisesPackageId, onPremisesVersion));
        }

        if (packages.All.TryGetValue(CloudPackageId, out string? cloudVersion))
        {
            found.Add(new DetectedPackage(DeployPackage.Cloud, CloudPackageId, cloudVersion));
        }

        return found.Count switch
        {
            1 => found[0],
            0 => throw new UsageException(
                $"'{Path.GetFileName(projectPath)}' doesn't reference {OnPremisesPackageId} or {CloudPackageId}.",
                $"Install Umbraco Deploy first, for example with 'dotnet add package {OnPremisesPackageId}'."),
            _ => throw new UsageException(
                $"'{Path.GetFileName(projectPath)}' references both {OnPremisesPackageId} and {CloudPackageId}; a site must use only one of them."),
        };
    }
}
