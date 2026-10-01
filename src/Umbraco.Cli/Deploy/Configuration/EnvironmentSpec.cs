using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// An Environment given on the command line as <c>Name=…;Type=…;Url=…[;Id=…]</c>.
/// </summary>
internal sealed record EnvironmentSpec(string Name, string Type, string Url, Guid? Id)
{
    public const string Format = "Name=<name>;Type=<development|staging|live|…>;Url=<https://…>[;Id=<guid>]";

    private static readonly string[] Keys = ["Name", "Type", "Url", "Id"];

    public static EnvironmentSpec Parse(string value)
    {
        var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw Invalid(value, $"'{part}' is not a key=value pair");
            }

            string key = part[..separator].Trim();
            if (Keys.Contains(key, StringComparer.OrdinalIgnoreCase) is false)
            {
                throw Invalid(value, $"'{key}' is not one of Name, Type, Url or Id");
            }

            parts[key] = part[(separator + 1)..].Trim();
        }

        string name = Required(parts, "Name", value);
        string type = Required(parts, "Type", value).ToLowerInvariant();
        string url = Required(parts, "Url", value);

        if (GetUrlError(url) is string urlError)
        {
            throw Invalid(value, urlError);
        }

        Guid? id = null;
        if (parts.TryGetValue("Id", out string? idValue))
        {
            id = Guid.TryParse(idValue, out Guid parsed) && parsed != Guid.Empty ? parsed : throw Invalid(value, $"'{idValue}' is not a GUID");
        }

        return new EnvironmentSpec(name, type, url.TrimEnd('/'), id);
    }

    /// <summary>
    /// Gets why <paramref name="url" /> can't be the URL of an Environment (an absolute HTTPS URL, or HTTP for localhost), or <c>null</c> if it can.
    /// </summary>
    public static string? GetUrlError(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) is false || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return $"'{url}' is not an absolute URL";
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.IsLoopback is false)
        {
            return $"'{url}' must use HTTPS (only localhost may use HTTP)";
        }

        return null;
    }

    private static string Required(Dictionary<string, string> parts, string key, string value)
        => parts.TryGetValue(key, out string? part) && part.Length > 0 ? part : throw Invalid(value, $"{key} is missing");

    private static UsageException Invalid(string value, string reason)
        => new($"Invalid --environment '{value}': {reason}.", $"Use --environment \"{Format}\".");
}
