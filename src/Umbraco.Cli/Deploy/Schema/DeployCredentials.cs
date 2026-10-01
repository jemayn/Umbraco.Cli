namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The key that signs requests to Umbraco Deploy: the decoded ApiSecret, or the UTF-8 bytes of the obsolete ApiKey.
/// </summary>
internal sealed class DeployCredentials
{
    private readonly byte[] _key;

    public DeployCredentials(byte[] key, string source)
    {
        _key = key;
        Source = source;
    }

    /// <summary>
    /// Gets where the key was found, such as <c>--api-secret</c> or <c>umbraco-cloud.json</c> (for messages).
    /// </summary>
    public string Source { get; }

    public ReadOnlySpan<byte> Key => _key;

    // Never print the key, not even in debug output
    public override string ToString() => $"DeployCredentials {{ Source = {Source} }}";
}
