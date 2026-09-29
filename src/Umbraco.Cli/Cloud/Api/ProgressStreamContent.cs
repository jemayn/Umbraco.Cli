using System.Net;

namespace Umbraco.Cli.Cloud.Api;

/// <summary>
/// Streams a file as HTTP content and reports the number of bytes written.
/// </summary>
/// <remarks>
/// The length is known up front, so the request is not sent chunked. Progress counts bytes handed to the network stack, so it reaches 100% before the server responds.
/// </remarks>
internal sealed class ProgressStreamContent : HttpContent
{
    private const int BufferSize = 81920;

    private readonly string _path;
    private readonly IProgress<long>? _progress;

    public ProgressStreamContent(string path, IProgress<long>? progress)
    {
        _path = path;
        _progress = progress;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await using FileStream file = File.OpenRead(_path);
        byte[] buffer = new byte[BufferSize];
        long sent = 0;
        _progress?.Report(0);

        int read;
        while ((read = await file.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sent += read;
            _progress?.Report(sent);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = new FileInfo(_path).Length;
        return true;
    }
}
