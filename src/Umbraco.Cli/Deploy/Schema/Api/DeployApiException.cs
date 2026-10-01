using System.Net;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Deploy.Schema.Api;

/// <summary>
/// A failed request to Umbraco Deploy's Schema API: an error response, or no response at all (<see cref="StatusCode" /> is <c>null</c>).
/// </summary>
internal sealed class DeployApiException : CliException
{
    public DeployApiException(HttpStatusCode? statusCode, string message, string? hint = null, Exception? innerException = null)
        : base(message, statusCode == HttpStatusCode.Unauthorized ? ExitCodes.AuthenticationFailed : ExitCodes.Error, hint, innerException)
        => StatusCode = statusCode;

    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets a value indicating whether the request may succeed when tried again: no response, too many requests or a server error.
    /// </summary>
    public bool IsTransient => StatusCode is null or HttpStatusCode.TooManyRequests || (int)StatusCode >= 500;
}
