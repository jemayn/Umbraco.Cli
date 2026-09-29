using NUnit.Framework;
using Umbraco.Cli.Deploy;
using Umbraco.Cli.Deploy.ProjectDetection;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class DeployPackageDetectorTests
{
    [Test]
    public void FindDeployPackage_TopLevelOnPremises()
    {
        DetectedPackage package = DeployPackageDetector.FindDeployPackage(PackageList(topLevel: [("Umbraco.Cms", "17.7.0"), ("Umbraco.Deploy.OnPrem", "17.1.0")]), "Site.csproj");

        Assert.That(package, Is.EqualTo(new DetectedPackage(DeployPackage.OnPremises, "Umbraco.Deploy.OnPrem", "17.1.0")));
    }

    [Test]
    public void FindDeployPackage_TransitiveCloud()
    {
        DetectedPackage package = DeployPackageDetector.FindDeployPackage(PackageList(topLevel: [("Umbraco.Cloud.Cms", "17.2.3")], transitive: [("umbraco.deploy.cloud", "17.0.4")]), "Site.csproj");

        Assert.That(package.Package, Is.EqualTo(DeployPackage.Cloud));
        Assert.That(package.Version, Is.EqualTo("17.0.4"));
    }

    [Test]
    public void FindDeployPackage_Neither_ThrowsUsageException()
    {
        UsageException exception = Assert.Throws<UsageException>(() => DeployPackageDetector.FindDeployPackage(PackageList(topLevel: [("Umbraco.Cms", "17.7.0")]), "Site.csproj"))!;

        Assert.That(exception.Message, Does.Contain("doesn't reference"));
    }

    [Test]
    public void FindDeployPackage_Both_ThrowsUsageException()
    {
        UsageException exception = Assert.Throws<UsageException>(() => DeployPackageDetector.FindDeployPackage(
            PackageList(topLevel: [("Umbraco.Deploy.OnPrem", "17.1.0"), ("Umbraco.Deploy.Cloud", "17.1.0")]),
            "Site.csproj"))!;

        Assert.That(exception.Message, Does.Contain("both"));
    }

    [TestCase("10.0.401")]
    [TestCase("11.0.100-preview.1")]
    public void EnsureSupportedSdk_Sdk10OrLater_Passes(string version)
        => Assert.DoesNotThrow(() => DeployPackageDetector.EnsureSupportedSdk(version));

    [TestCase("8.0.404")]
    [TestCase("garbage")]
    public void EnsureSupportedSdk_OlderSdk_Throws(string version)
    {
        CliException exception = Assert.Throws<CliException>(() => DeployPackageDetector.EnsureSupportedSdk(version))!;

        Assert.That(exception.Message, Does.Contain(".NET SDK 10 or later is required").And.Contain(version).And.Contain("global.json"));
    }

    /// <summary>
    /// Builds output shaped like <c>dotnet package list --include-transitive --format json</c> (version 1).
    /// </summary>
    internal static string PackageList((string Id, string Version)[] topLevel, (string Id, string Version)[]? transitive = null)
    {
        static string Packages((string Id, string Version)[] packages)
            => string.Join(",", packages.Select(package => $$"""{ "id": "{{package.Id}}", "requestedVersion": "{{package.Version}}", "resolvedVersion": "{{package.Version}}" }"""));

        return $$"""
            {
              "version": 1,
              "parameters": "--include-transitive",
              "projects": [
                {
                  "path": "Site.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [ {{Packages(topLevel)}} ],
                      "transitivePackages": [ {{Packages(transitive ?? [])}} ]
                    }
                  ]
                }
              ]
            }
            """;
    }
}
