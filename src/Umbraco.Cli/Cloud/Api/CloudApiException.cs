using System.Net;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// An error response from the Umbraco Cloud API.
/// </summary>
internal sealed class CloudApiException : CliException
{
    public CloudApiException(HttpStatusCode statusCode, string message, string? hint = null)
        : base(message, statusCode == HttpStatusCode.Unauthorized ? ExitCodes.AuthenticationFailed : ExitCodes.Error, hint)
        => StatusCode = statusCode;

    public HttpStatusCode StatusCode { get; }
}
