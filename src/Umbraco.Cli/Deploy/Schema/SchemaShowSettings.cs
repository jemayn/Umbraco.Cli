namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The settings of <c>umbraco deploy schema show</c>.
/// </summary>
internal sealed record SchemaShowSettings
{
    public required SchemaTargetSettings Target { get; init; }

    /// <summary>
    /// Gets the task ID of the schema deployment, or <c>null</c> for the current (or last) one.
    /// </summary>
    public Guid? TaskId { get; init; }

    public bool Wait { get; init; }

    public TimeSpan Timeout { get; init; } = SchemaDeploymentWaiter.DefaultTimeout;
}
