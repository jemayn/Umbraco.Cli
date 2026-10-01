namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The settings of <c>umbraco deploy schema deploy</c>.
/// </summary>
internal sealed record SchemaDeploySettings
{
    public required SchemaTargetSettings Target { get; init; }

    public string Reason { get; init; } = SchemaCommands.DefaultReason;

    public bool NoWait { get; init; }

    public TimeSpan Timeout { get; init; } = SchemaDeploymentWaiter.DefaultTimeout;
}
