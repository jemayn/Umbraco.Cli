namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The options that select the Environment of a schema deployment (its credentials come from environment variables or the configuration files).
/// </summary>
internal sealed record SchemaTargetSettings
{
    /// <summary>
    /// Gets the <c>appsettings.json</c> or <c>umbraco-cloud.json</c> to read the credentials and Environments from, or a folder containing them (default: the current directory, or the Umbraco project in the repository).
    /// </summary>
    public string? Config { get; init; }

    public string? Environment { get; init; }

    public string? Url { get; init; }
}
