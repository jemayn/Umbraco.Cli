namespace Umbraco.Cli.Deploy.Configuration;

/// <summary>
/// The Baseline connection changes <c>umbraco deploy init</c> was asked to make.
/// </summary>
/// <param name="Url">The URL of the Baseline project's Live environment to set (Child project).</param>
/// <param name="Secret">The Baseline secret to set (Child project).</param>
/// <param name="RotateSecret">Whether to generate a new Baseline secret (Child project).</param>
/// <param name="Remove">Whether to remove the Baseline URL and Baseline secret (Child project).</param>
/// <param name="ChildSecretName">The name of the child secret to generate (Baseline project).</param>
/// <param name="RemoveChildSecretName">The name of the child secret to remove (Baseline project).</param>
internal sealed record BaselineOptions(string? Url = null, string? Secret = null, bool RotateSecret = false, bool Remove = false, string? ChildSecretName = null, string? RemoveChildSecretName = null)
{
    public bool IsEmpty => Url is null && Secret is null && RotateSecret is false && Remove is false && ChildSecretName is null && RemoveChildSecretName is null;
}
