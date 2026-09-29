namespace Umbraco.Cli.Deploy;

/// <summary>
/// The Umbraco Deploy package a site uses, which decides where its configuration is written.
/// </summary>
internal enum DeployPackage
{
    /// <summary>
    /// Umbraco Deploy On-Premises (<c>Umbraco.Deploy.OnPrem</c>), configured in <c>appsettings.json</c>.
    /// </summary>
    OnPremises,

    /// <summary>
    /// Umbraco Deploy Cloud (<c>Umbraco.Deploy.Cloud</c>), configured in <c>umbraco-cloud.json</c>.
    /// </summary>
    Cloud,
}
