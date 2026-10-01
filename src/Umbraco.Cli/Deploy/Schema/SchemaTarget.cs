namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The Environment a schema deployment runs on: its base URL (ending with <c>/</c>), its name when it was given by name, and the credentials.
/// </summary>
internal sealed record SchemaTarget(Uri BaseUrl, string? Environment, DeployCredentials Credentials)
{
    /// <summary>
    /// Gets the Environment name, or the URL without the trailing <c>/</c> (for messages).
    /// </summary>
    public string DisplayName => Environment ?? Url;

    public string Url => BaseUrl.AbsoluteUri.TrimEnd('/');
}
