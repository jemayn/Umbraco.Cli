using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Deploy.Schema.Api;

/// <inheritdoc />
internal sealed class DeployApiClient : IDeployApiClient
{
    public const string StartPath = "umbraco/umbracodeploy/extract/start/";

    public const string StatusPath = "umbraco/umbracodeploy/statusreport/";

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly IReporter _reporter;

    public DeployApiClient(HttpClient httpClient, TimeProvider timeProvider, IReporter reporter)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _reporter = reporter;
    }

    /// <summary>
    /// Gets the timeout of each request (settable for tests).
    /// </summary>
    internal TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(100);

    public async Task<ExtractStartResponse> StartAsync(SchemaTarget target, string reason, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync(target, HttpMethod.Post, StartPath + Uri.EscapeDataString(reason), DeployJsonContext.Default.ExtractStartResponse, cancellationToken);
        }
        catch (DeployApiException exception) when (exception.StatusCode is null && exception.InnerException is OperationCanceledException)
        {
            // Not retried: the request may have reached Umbraco Deploy, and a retry could start a second schema deployment
            throw new CliException(
                $"Umbraco Deploy on {target.DisplayName} didn't respond within {RequestTimeout.TotalSeconds:0} seconds to starting the schema deployment; it may have started anyway.",
                ExitCodes.Error,
                "Run 'umbraco deploy schema show' with the same target options before trying again.",
                exception);
        }
    }

    public Task<WorkStatusResponse> GetStatusAsync(SchemaTarget target, Guid? taskId, CancellationToken cancellationToken)
        => SendAsync(target, HttpMethod.Get, StatusPath + (taskId is Guid id ? $"get/{id}" : "getcurrent"), DeployJsonContext.Default.WorkStatusResponse, cancellationToken);

    private static string FormatProblem(ProblemDetailsResponse? problem)
    {
        string? text = problem?.Detail ?? problem?.Title;
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $" ({text.Trim().TrimEnd('.')})";
    }

    private static async Task<ProblemDetailsResponse?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(DeployJsonContext.Default.ProblemDetailsResponse, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            // Not a JSON response
            return null;
        }
    }

    private async Task<T> SendAsync<T>(SchemaTarget target, HttpMethod method, string relativePath, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var uri = new Uri(target.BaseUrl, relativePath);
        using var request = new HttpRequestMessage(method, uri);

        // A fresh token per request, as Umbraco Deploy only accepts it within its session timeout
        string token = DeployBearerToken.Create(target.Credentials.Key, uri, _timeProvider.GetUtcNow(), Guid.NewGuid().ToString());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, timeout.Token);
            return await ReadAsync(target, response, typeInfo, timeout.Token);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested is false)
        {
            throw new DeployApiException(null, $"Umbraco Deploy on {target.DisplayName} didn't respond within {RequestTimeout.TotalSeconds:0} seconds.", innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            // Not Umbraco Cloud, which the error handler would otherwise name
            throw new DeployApiException(null, $"Could not reach {target.Url}: {exception.Message}", innerException: exception);
        }
    }

    private async Task<T> ReadAsync<T>(SchemaTarget target, HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        _reporter.Verbose($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath} responded {(int)response.StatusCode} {response.ReasonPhrase}");

        if (response.IsSuccessStatusCode is false)
        {
            ProblemDetailsResponse? problem = await TryReadProblemAsync(response, cancellationToken);
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new DeployApiException(
                    response.StatusCode,
                    $"Umbraco Deploy on {target.DisplayName} rejected the credentials (401 Unauthorized).",
                    $"Check that the ApiSecret (or ApiKey) from {target.Credentials.Source} is the one configured on the Environment, and that the clocks differ by less than 20 minutes."),
                HttpStatusCode.Forbidden => new DeployApiException(
                    response.StatusCode,
                    $"Umbraco Deploy on {target.DisplayName} refused the request (403 Forbidden){FormatProblem(problem)}.",
                    "Umbraco Deploy requires HTTPS when the site does (Umbraco:CMS:Global:UseHttps); use an https:// URL."),
                HttpStatusCode.NotFound => new DeployApiException(
                    response.StatusCode,
                    $"Umbraco Deploy's Schema API wasn't found at {target.Url} (404 Not Found).",
                    "Check the URL of the Environment, and that the site runs Umbraco Deploy."),
                _ => new DeployApiException(
                    response.StatusCode,
                    $"Umbraco Deploy on {target.DisplayName} responded {(int)response.StatusCode} {response.ReasonPhrase}{FormatProblem(problem)}."),
            };
        }

        T? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new CliException($"Umbraco Deploy on {target.DisplayName} returned an unexpected response ({(int)response.StatusCode}): {exception.Message}", ExitCodes.Error, "Check the URL of the Environment.", exception);
        }

        return result ?? throw new CliException($"Umbraco Deploy on {target.DisplayName} returned an empty response ({(int)response.StatusCode}).");
    }
}
