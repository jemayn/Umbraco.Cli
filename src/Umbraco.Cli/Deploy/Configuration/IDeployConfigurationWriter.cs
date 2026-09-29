using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// Applies <c>umbraco deploy init</c> to the configuration file of one Umbraco Deploy package.
/// </summary>
internal interface IDeployConfigurationWriter
{
    DeployPackage Package { get; }

    /// <summary>
    /// Gets the name of the configuration file, next to the project file.
    /// </summary>
    string FileName { get; }

    InitOutcome Apply(JsonDocumentEditor document, InitRequest request);
}
