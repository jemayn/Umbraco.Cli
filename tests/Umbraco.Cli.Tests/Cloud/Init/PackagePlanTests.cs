using NUnit.Framework;
using Umbraco.Cli.Cloud.Init;

namespace Umbraco.Cli.Tests.Cloud.Init;

[TestFixture]
public class PackagePlanTests
{
    private static readonly ProductCatalog Catalog = ProductCatalog.LoadEmbedded();

    [Test]
    public void EmbeddedCatalog_HasTheRequiredPackagesAndForms()
    {
        Assert.That(Catalog.RequiredPackages, Is.EqualTo(new[] { "Umbraco.Cloud.Cms", "Umbraco.Cloud.StorageProviders.AzureBlob" }));
        Assert.That(Catalog.FindProduct("deploy")!.Required, Is.True);
        Assert.That(Catalog.FindProduct("FORMS")!.Packages, Is.EqualTo(new[] { "Umbraco.Forms" }));
        Assert.That(Catalog.Scaffold.VerifiedMajors, Does.Contain(17).And.Contain(18));
    }

    [Test]
    public void Create_WithoutSelection_PlansTheRequiredPackages()
    {
        PackagePlan plan = PackagePlan.Create(Catalog, [], References());

        Assert.That(plan.Packages.Select(package => (package.Id, package.Reason)), Is.EqualTo(new[]
        {
            ("Umbraco.Cloud.Cms", "required"),
            ("Umbraco.Cloud.StorageProviders.AzureBlob", "required"),
            ("Umbraco.Deploy.Cloud", "required"),
        }));
        Assert.That(plan.PresentProducts.Select(product => product.Key), Is.EqualTo(new[] { "deploy" }));
    }

    [Test]
    public void Create_WithForms_AddsFormsAndItsIntegrationPackage()
    {
        PackagePlan plan = PackagePlan.Create(Catalog, ["forms"], References());

        Assert.That(plan.Packages.Select(package => (package.Id, package.Reason)), Does.Contain(("Umbraco.Forms", "--add forms")).And.Contain(("Umbraco.Forms.Deploy", "integration")));
        Assert.That(plan.PresentProducts.Select(product => product.LicenseKey), Is.EqualTo(new[] { "Umbraco.Deploy", "Umbraco.Forms" }));
    }

    [Test]
    public void Create_FormsAlreadyReferenced_AddsTheIntegrationPackageAndKeepsForms()
    {
        PackagePlan plan = PackagePlan.Create(Catalog, [], References(("Umbraco.Forms", "17.5.2"), ("umbraco.cloud.cms", "17.0.0")));

        Assert.That(plan.Packages.Single(package => package.Id == "Umbraco.Forms.Deploy").IsReferenced, Is.False);
        Assert.That(plan.Packages.Single(package => package.Id == "Umbraco.Cloud.Cms").ExistingVersion, Is.EqualTo("17.0.0"));
        Assert.That(plan.Packages.Any(package => package.Id == "Umbraco.Forms"), Is.False);
        Assert.That(plan.PresentProducts.Select(product => product.Key), Does.Contain("forms"));
    }

    private static Dictionary<string, string> References(params (string Id, string Version)[] references)
        => references.ToDictionary(reference => reference.Id, reference => reference.Version, StringComparer.OrdinalIgnoreCase);
}
