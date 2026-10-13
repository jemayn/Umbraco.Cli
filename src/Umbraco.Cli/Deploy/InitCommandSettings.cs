namespace Umbraco.Cli.Deploy;

/// <summary>
/// The options of <c>umbraco deploy init</c>.
/// </summary>
internal sealed record InitCommandSettings
{
    public string? Project { get; init; }

    public IReadOnlyList<string> Environments { get; init; } = [];

    public string? CurrentEnvironment { get; init; }

    public string? ProjectId { get; init; }

    public bool RotateSecret { get; init; }

    public string? BaselineUrl { get; init; }

    public string? BaselineSecret { get; init; }

    public bool RotateBaselineSecret { get; init; }

    public bool RemoveBaseline { get; init; }

    public string? ChildSecret { get; init; }

    public string? RemoveChildSecret { get; init; }

    public bool DryRun { get; init; }

    public bool NonInteractive { get; init; }
}
