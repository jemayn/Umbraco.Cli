namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// The packages to install and the licensed Products that are present afterwards.
/// </summary>
internal sealed record PackagePlan(IReadOnlyList<PlannedPackage> Packages, IReadOnlyList<ProductCatalog.Product> PresentProducts)
{
    /// <summary>
    /// Plans the packages: the required packages, the selected Products and the Integration packages whose Products are all present
    /// (selected, required or already referenced).
    /// </summary>
    /// <param name="catalog">The Product catalogue.</param>
    /// <param name="selectedKeys">The keys of the Products chosen with <c>--add</c> or at the prompt.</param>
    /// <param name="referenced">The top-level package references of the Umbraco project (ID to version).</param>
    public static PackagePlan Create(ProductCatalog catalog, IReadOnlyCollection<string> selectedKeys, IReadOnlyDictionary<string, string> referenced)
    {
        var packages = new List<PlannedPackage>();
        void Add(string id, string reason)
        {
            if (packages.Any(package => string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase)) is false)
            {
                packages.Add(new PlannedPackage(id, reason, referenced.TryGetValue(id, out string? version) ? version : null));
            }
        }

        foreach (string id in catalog.RequiredPackages)
        {
            Add(id, PlannedPackage.RequiredReason);
        }

        var present = new List<ProductCatalog.Product>();
        foreach (ProductCatalog.Product product in catalog.Products)
        {
            bool selected = selectedKeys.Contains(product.Key, StringComparer.OrdinalIgnoreCase);
            if (product.Required || selected)
            {
                foreach (string id in product.Packages)
                {
                    Add(id, product.Required ? PlannedPackage.RequiredReason : $"--add {product.Key}");
                }

                present.Add(product);
            }
            else if (product.Packages.All(referenced.ContainsKey))
            {
                present.Add(product);
            }
        }

        foreach (ProductCatalog.IntegrationPackage integration in catalog.IntegrationPackages)
        {
            if (integration.Products.All(key => present.Any(product => string.Equals(product.Key, key, StringComparison.OrdinalIgnoreCase))))
            {
                Add(integration.Package, PlannedPackage.IntegrationReason);
            }
        }

        return new PackagePlan(packages, present);
    }
}
