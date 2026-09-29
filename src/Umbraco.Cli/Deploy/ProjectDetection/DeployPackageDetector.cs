using System.Globalization;
using System.Text.Json;
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
        var found = new Dictionary<DeployPackage, DetectedPackage>();
        using JsonDocument document = JsonDocument.Parse(packageListJson);
        foreach (JsonElement package in EnumeratePackages(document.RootElement))
        {
            string? id = package.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
            string version = package.TryGetProperty("resolvedVersion", out JsonElement versionElement) ? versionElement.GetString() ?? string.Empty : string.Empty;
            if (string.Equals(id, OnPremisesPackageId, StringComparison.OrdinalIgnoreCase))
            {
                found.TryAdd(DeployPackage.OnPremises, new DetectedPackage(DeployPackage.OnPremises, OnPremisesPackageId, version));
            }
            else if (string.Equals(id, CloudPackageId, StringComparison.OrdinalIgnoreCase))
            {
                found.TryAdd(DeployPackage.Cloud, new DetectedPackage(DeployPackage.Cloud, CloudPackageId, version));
            }
        }

        return found.Count switch
        {
            1 => found.Values.Single(),
            0 => throw new UsageException(
                $"'{Path.GetFileName(projectPath)}' doesn't reference {OnPremisesPackageId} or {CloudPackageId}.",
                $"Install Umbraco Deploy first, for example with 'dotnet add package {OnPremisesPackageId}'."),
            _ => throw new UsageException(
                $"'{Path.GetFileName(projectPath)}' references both {OnPremisesPackageId} and {CloudPackageId}; a site must use only one of them."),
        };
    }

    private static IEnumerable<JsonElement> EnumeratePackages(JsonElement root)
    {
        if (root.TryGetProperty("projects", out JsonElement projects) is false || projects.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement project in projects.EnumerateArray())
        {
            if (project.TryGetProperty("frameworks", out JsonElement frameworks) is false || frameworks.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement framework in frameworks.EnumerateArray())
            {
                foreach (string list in new[] { "topLevelPackages", "transitivePackages" })
                {
                    if (framework.TryGetProperty(list, out JsonElement packages) && packages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement package in packages.EnumerateArray())
                        {
                            yield return package;
                        }
                    }
                }
            }
        }
    }
}
