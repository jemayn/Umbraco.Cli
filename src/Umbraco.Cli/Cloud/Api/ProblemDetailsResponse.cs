namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// The error body returned by the Umbraco Cloud API.
/// </summary>
internal sealed record ProblemDetailsResponse
{
    public string? Title { get; set; }

    public int? Status { get; set; }

    public string? Detail { get; set; }
}
