using System.Security.Cryptography;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// The Umbraco Deploy ApiSecret: a cryptographically random value of at least 64 bytes, Base64-encoded.
/// </summary>
internal static class ApiSecret
{
    public const int MinimumLength = 64;

    /// <summary>
    /// Generates 64 random bytes, Base64-encoded (88 characters).
    /// </summary>
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(MinimumLength));

    /// <summary>
    /// Checks that a configured value is Base64 for at least 64 bytes, as Umbraco Deploy requires.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        byte[] buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out int length) && length >= MinimumLength;
    }
}
