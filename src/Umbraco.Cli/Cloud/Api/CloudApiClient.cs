using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Cloud.Api;

/// <inheritdoc />
internal sealed class CloudApiClient : ICloudApiClient
{
    public const string ApiKeyHeaderName = "Umbraco-Cloud-Api-Key";

    public const string DefaultBaseAddress = "https://api.cloud.umbraco.com/";

    public const int MaxPollAttempts = 5;

    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(25);

    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly IReporter _reporter;

    public CloudApiClient(HttpClient httpClient, TimeProvider timeProvider, IReporter reporter)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _reporter = reporter;
    }

    /// <summary>
    /// Gets the timeout of requests other than uploads (settable for tests).
    /// </summary>
    internal TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(100);

    public async Task<ArtifactResponse> UploadArtifactAsync(
        CloudCredentials credentials,
        string zipPath,
        string? description,
        string? version,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var file = new ProgressStreamContent(zipPath, progress);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(file, "file", Path.GetFileName(zipPath));

        if (description is not null)
        {
            content.Add(new StringContent(description), "description");
        }

        if (version is not null)
        {
            content.Add(new StringContent(version), "version");
        }

        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, credentials, "deployments/artifacts");
        request.Content = content;

        // The upload itself has no timeout (it can take long on slow connections), so only cancellation stops it
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync(response, CloudJsonContext.Default.ArtifactResponse, cancellationToken);
    }

    public async Task<DeploymentResponse> StartDeploymentAsync(CloudCredentials credentials, StartDeploymentRequest request, CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, credentials, "deployments");
        message.Content = JsonContent.Create(request, CloudJsonContext.Default.StartDeploymentRequest);

        using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested is false)
        {
            // Not retried: the request may have reached Umbraco Cloud, and a retry could queue a second deployment
            throw new CliException(
                $"Umbraco Cloud didn't respond within {RequestTimeout.TotalSeconds:0} seconds to starting the deployment; it may have started anyway.",
                ExitCodes.Error,
                "Check the deployments of the project in the Umbraco Cloud Portal before trying again.",
                exception);
        }

        using HttpResponseMessage ownedResponse = response;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ProblemDetailsResponse? problem = await TryReadProblemAsync(response, cancellationToken);
            throw new CloudApiException(
                response.StatusCode,
                $"The deployment could not be started, because another deployment is in progress{FormatProblem(problem)}.",
                "Wait for the running deployment to finish, then try again.");
        }

        return await ReadAsync(response, CloudJsonContext.Default.DeploymentResponse, cancellationToken);
    }

    public async Task<DeploymentResponse> GetDeploymentAsync(CloudCredentials credentials, string deploymentId, DateTimeOffset? lastModifiedUtc, CancellationToken cancellationToken)
    {
        string path = $"deployments/{Uri.EscapeDataString(deploymentId)}";
        if (lastModifiedUtc is not null)
        {
            path += "?lastModifiedUtc=" + Uri.EscapeDataString(lastModifiedUtc.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        }

        for (int attempt = 1; ; attempt++)
        {
            TimeSpan? retryAfter;
            string failure;
            try
            {
                using HttpRequestMessage request = CreateRequest(HttpMethod.Get, credentials, path);
                using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, timeout.Token);
                if (IsTransient(response.StatusCode) is false || attempt == MaxPollAttempts)
                {
                    return await ReadAsync(response, CloudJsonContext.Default.DeploymentResponse, cancellationToken);
                }

                retryAfter = GetRetryAfter(response);
                failure = $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken))
            {
                if (attempt == MaxPollAttempts)
                {
                    throw new CliException($"Could not get the status of deployment {deploymentId}: {exception.Message}", ExitCodes.Error, innerException: exception);
                }

                retryAfter = null;
                failure = exception.Message;
            }

            // Honour Retry-After, but never sleep so long that the wait's own timeout is overshot by much
            TimeSpan delay = retryAfter is TimeSpan requested ? (requested > MaxRetryAfter ? MaxRetryAfter : requested) : GetBackoff(attempt);
            _reporter.Verbose($"Getting the deployment status failed ({failure}), retrying in {delay.TotalSeconds:0} seconds (attempt {attempt} of {MaxPollAttempts}).");
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the delay before the next attempt: 5 seconds, doubling, up to the 25 second poll interval.
    /// </summary>
    internal static TimeSpan GetBackoff(int attempt)
    {
        double seconds = InitialRetryDelay.TotalSeconds * Math.Pow(2, attempt - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException || (exception is OperationCanceledException && cancellationToken.IsCancellationRequested is false);

    private static string FormatProblem(ProblemDetailsResponse? problem)
    {
        string? text = problem?.Detail ?? problem?.Title;
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $" ({text.Trim().TrimEnd('.')})";
    }

    private static async Task<ProblemDetailsResponse?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(CloudJsonContext.Default.ProblemDetailsResponse, cancellationToken);
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

    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            TimeSpan delay = date - _timeProvider.GetUtcNow();
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        return timeout;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, CloudCredentials credentials, string relativePath)
    {
        var request = new HttpRequestMessage(method, $"v2/projects/{Uri.EscapeDataString(credentials.ProjectId)}/{relativePath}");
        request.Headers.Add(ApiKeyHeaderName, credentials.ApiKey);
        return request;
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        _reporter.Verbose($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath} responded {(int)response.StatusCode} {response.ReasonPhrase}");

        if (response.IsSuccessStatusCode is false)
        {
            ProblemDetailsResponse? problem = await TryReadProblemAsync(response, cancellationToken);
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new CloudApiException(
                    response.StatusCode,
                    "Umbraco Cloud rejected the credentials (401 Unauthorized).",
                    "Check the project ID and CI/CD API key (--project-id/UMBRACO_CLOUD_PROJECT_ID and --api-key/UMBRACO_CLOUD_API_KEY)."),
                _ => new CloudApiException(
                    response.StatusCode,
                    $"Umbraco Cloud responded {(int)response.StatusCode} {response.ReasonPhrase}{FormatProblem(problem)}."),
            };
        }

        T? result = await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken);
        return result ?? throw new CliException($"Umbraco Cloud returned an empty response ({(int)response.StatusCode}).");
    }
}
