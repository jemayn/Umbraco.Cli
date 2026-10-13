namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// What <c>umbraco deploy init</c> was asked to configure.
/// </summary>
/// <param name="Environments">The Environments that replace the configured list, or <c>null</c> to leave the list untouched.</param>
/// <param name="CurrentEnvironment">The name of the current environment (Umbraco Deploy On-Premises only).</param>
/// <param name="ProjectId">The Umbraco Cloud project ID (Umbraco Deploy Cloud only).</param>
/// <param name="RotateSecret">Whether to replace a valid ApiSecret (or, on-premises, an ApiKey).</param>
/// <param name="Baseline">The Baseline connection changes (Umbraco Deploy On-Premises only), or <c>null</c> for none.</param>
internal sealed record InitRequest(IReadOnlyList<EnvironmentSpec>? Environments, string? CurrentEnvironment, Guid? ProjectId, bool RotateSecret, BaselineOptions? Baseline = null);
