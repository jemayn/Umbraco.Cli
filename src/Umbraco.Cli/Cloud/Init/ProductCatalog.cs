using System.Text.Json;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The packages <c>umbraco cloud init</c> installs: the required packages, the Products and their Integration packages.
/// </summary>
internal sealed class ProductCatalog
{
    public List<string> RequiredPackages { get; set; } = [];

    public List<Product> Products { get; set; } = [];

    public List<IntegrationPackage> IntegrationPackages { get; set; } = [];

    public ScaffoldSettings Scaffold { get; set; } = new();

    /// <summary>
    /// Loads the catalogue embedded in the CLI.
    /// </summary>
    public static ProductCatalog LoadEmbedded()
    {
        using Stream stream = typeof(ProductCatalog).Assembly.GetManifestResourceStream("catalog.json")
            ?? throw new InvalidOperationException("The embedded Product catalogue is missing.");

        return JsonSerializer.Deserialize(stream, InitJsonContext.Default.ProductCatalog)
            ?? throw new InvalidOperationException("The embedded Product catalogue is empty.");
    }

    public Product? FindProduct(string key) => Products.FirstOrDefault(product => string.Equals(product.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A Product: an installable Umbraco add-on made up of one or more packages.
    /// </summary>
    internal sealed class Product
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<string> Packages { get; set; } = [];

        /// <summary>
        /// Gets or sets the product key in <c>Umbraco:Licenses:Products</c> that is set to <c>UMBRACO-CLOUD</c>, if the Product is licensed.
        /// </summary>
        public string? LicenseKey { get; set; }

        public bool Required { get; set; }
    }

    /// <summary>
    /// A package that makes two or more Products work together.
    /// </summary>
    internal sealed class IntegrationPackage
    {
        public string Package { get; set; } = string.Empty;

        public List<string> Products { get; set; } = [];
    }

    /// <summary>
    /// The majors the embedded Cloud scaffold files were verified against.
    /// </summary>
    internal sealed class ScaffoldSettings
    {
        public List<int> VerifiedMajors { get; set; } = [];
    }
}
