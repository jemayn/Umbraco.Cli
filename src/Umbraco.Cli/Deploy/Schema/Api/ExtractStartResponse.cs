namespace Umbraco.Cli.Deploy.Schema.Api;

/// <summary>
/// The response to starting a schema deployment (Umbraco Deploy's extraction).
/// </summary>
internal sealed record ExtractStartResponse
{
    public Guid TaskId { get; set; }

    public bool TaskStarted { get; set; }
}
