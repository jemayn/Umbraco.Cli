namespace Umbraco.Cli.Deploy.Schema.Api;

/// <summary>
/// Calls Umbraco Deploy's Schema API on an Environment, authenticated with its ApiSecret (or obsolete ApiKey).
/// </summary>
internal interface IDeployApiClient
{
    /// <summary>
    /// Starts a schema deployment (never retried, as a retry could start a second one).
    /// </summary>
    Task<ExtractStartResponse> StartAsync(SchemaTarget target, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the status of a schema deployment, or of the current (or last) task when <paramref name="taskId" /> is <c>null</c>.
    /// </summary>
    Task<WorkStatusResponse> GetStatusAsync(SchemaTarget target, Guid? taskId, CancellationToken cancellationToken);
}
