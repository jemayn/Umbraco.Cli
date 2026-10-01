namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The options that select the Environment of a schema deployment and its credentials.
/// </summary>
internal sealed record SchemaTargetSettings
{
    /// <summary>
    /// Gets the <c>appsettings.json</c> or <c>umbraco-cloud.json</c> to read the credentials and Environments from, or a folder containing them (default: the current directory).
    /// </summary>
    public string? Config { get; init; }

    public string? Environment { get; init; }

    public string? Url { get; init; }

    public string? ApiSecret { get; init; }

    public string? ApiKey { get; init; }
}
