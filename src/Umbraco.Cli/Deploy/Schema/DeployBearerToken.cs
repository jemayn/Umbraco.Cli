using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// Creates the HMAC bearer token Umbraco Deploy accepts on its Schema API (as <c>TriggerDeploy.ps1</c> does).
/// </summary>
/// <remarks>
/// The token is <c>base64("{signature}:{nonce}:{timestamp}")</c>, where the signature is the Base64 HMAC-SHA256 of the request's path and
/// query, the Unix timestamp in seconds and the nonce. Umbraco Deploy accepts it while the timestamp is within its session timeout (20 minutes by default).
/// </remarks>
internal static class DeployBearerToken
{
    public static string Create(ReadOnlySpan<byte> key, Uri requestUri, DateTimeOffset now, string nonce)
    {
        // Umbraco Deploy signs the path and query as received, with any double slashes collapsed
        string pathAndQuery = requestUri.PathAndQuery.Replace("//", "/", StringComparison.Ordinal);
        string timestamp = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        byte[] hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(pathAndQuery + timestamp + nonce));
        string signature = Convert.ToBase64String(hash);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{signature}:{nonce}:{timestamp}"));
    }
}
