using System.Net;
using System.Text;

namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// Returns queued responses and records the requests (including their bodies).
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHttpMessageHandler Respond(HttpStatusCode statusCode, string? json = null, Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(statusCode);
            if (json is not null)
            {
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            configure?.Invoke(response);
            return response;
        });

        return this;
    }

    /// <summary>
    /// Never responds (until the request is cancelled).
    /// </summary>
    public FakeHttpMessageHandler Hang()
    {
        _responses.Enqueue(_ => null!);
        return this;
    }

    public FakeHttpMessageHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[] body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value)),
            request.Content?.Headers.ContentType?.MediaType,
            body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No response queued for {request.Method} {request.RequestUri}.");
        }

        HttpResponseMessage? response = _responses.Dequeue()(request);
        if (response is null)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        response!.RequestMessage = request;
        return response;
    }
}
