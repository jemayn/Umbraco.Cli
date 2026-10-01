using System.Text.Json.Serialization;
using Umbraco.Cli.Deploy.Schema.Api;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// The result of <c>umbraco deploy schema deploy|show</c>, as written with <c>--output json</c> (a public contract: don't rename properties).
/// </summary>
internal sealed record SchemaDeploymentResult
{
    public required Guid TaskId { get; init; }

    /// <summary>
    /// Gets the name of the Environment, when it was given by name.
    /// </summary>
    public string? Environment { get; init; }

    public required string Url { get; init; }

    /// <summary>
    /// Gets <c>New</c>, <c>Executing</c>, <c>Completed</c>, <c>Failed</c>, <c>Cancelled</c>, <c>TimedOut</c> or <c>Mismatch</c>.
    /// </summary>
    public required string Status { get; init; }

    public string? Comment { get; init; }

    public int? Percent { get; init; }

    public SchemaDeploymentException? Exception { get; init; }

    public string? Log { get; init; }

    public IReadOnlyList<string>? Mismatches { get; init; }

    /// <summary>
    /// Gets a value indicating whether the schema deployment was started again while waiting, because its status was no longer known.
    /// </summary>
    public bool Restarted { get; init; }

    [JsonIgnore]
    public bool IsFailed { get; init; }

    /// <summary>
    /// Gets a value indicating whether the schema deployment was only started (<c>--no-wait</c>).
    /// </summary>
    [JsonIgnore]
    public bool IsStarted { get; init; }

    public static SchemaDeploymentResult Started(SchemaTarget target, Guid taskId)
        => new() { TaskId = taskId, Environment = target.Environment, Url = target.Url, Status = WorkStatuses.New, IsStarted = true };

    public static SchemaDeploymentResult From(SchemaTarget target, Guid taskId, WorkStatusResponse status, bool restarted)
        => new()
        {
            TaskId = taskId,
            Environment = target.Environment,
            Url = target.Url,
            Status = status.Status,
            Comment = string.IsNullOrWhiteSpace(status.Comment) ? null : status.Comment.Trim(),
            Percent = status.Percent >= 0 ? status.Percent : null,
            Exception = status.Exception is { } exception ? new SchemaDeploymentException(exception.Type, exception.Detail) : null,
            Log = string.IsNullOrWhiteSpace(status.Log) ? null : status.Log,
            Mismatches = status.MismatchList is { Count: > 0 } mismatches ? mismatches.Select(mismatch => mismatch.Udi ?? mismatch.Alias ?? mismatch.Name ?? string.Empty).ToList() : null,
            Restarted = restarted,
            IsFailed = status.IsFailed,
        };
}

/// <summary>
/// The exception that made a schema deployment fail.
/// </summary>
internal sealed record SchemaDeploymentException(string? Type, string? Detail);
