using System.Net.Http;
using System.Text;

namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// A request received by <see cref="FakeHttpMessageHandler" />.
/// </summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? ContentType, byte[] Body)
{
    public string BodyText => Encoding.UTF8.GetString(Body);
}
