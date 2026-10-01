using System.Text.Json.Serialization;

namespace Umbraco.Cli.Deploy.Schema.Api;

/// <summary>
/// The status of an Umbraco Deploy task, as returned by its status report endpoints.
/// </summary>
/// <remarks>
/// Response types use settable properties: the source-generated serializer sets missing init-only properties to <c>null</c> instead of keeping their defaults.
/// </remarks>
internal sealed record WorkStatusResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = WorkStatuses.Unknown;

    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets the progress, or <c>-1</c> when it's unknown.
    /// </summary>
    public int Percent { get; set; } = -1;

    public WorkExceptionResponse? Exception { get; set; }

    /// <summary>
    /// Gets or sets the log of the task (only once it has completed or failed).
    /// </summary>
    public string? Log { get; set; }

    public IReadOnlyList<MismatchResponse>? MismatchList { get; set; }

    [JsonIgnore]
    public bool IsKnown => WorkStatuses.IsUnknown(Status) is false;

    [JsonIgnore]
    public bool IsCompleted => WorkStatuses.IsCompleted(Status);

    [JsonIgnore]
    public bool IsTerminal => IsKnown && WorkStatuses.IsRunning(Status) is false;

    [JsonIgnore]
    public bool IsFailed => IsTerminal && IsCompleted is false;
}

/// <summary>
/// An exception of a failed Umbraco Deploy task.
/// </summary>
internal sealed record WorkExceptionResponse
{
    public string? Type { get; set; }

    public string? Detail { get; set; }
}

/// <summary>
/// An item whose schema doesn't match.
/// </summary>
internal sealed record MismatchResponse
{
    public string? Udi { get; set; }

    public string? Name { get; set; }

    public string? Alias { get; set; }
}
