namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// A package in the result of <c>umbraco cloud init</c>.
/// </summary>
/// <param name="Id">The package ID.</param>
/// <param name="Version">The version added, or the version already referenced.</param>
/// <param name="Action"><c>added</c>, <c>referenced</c> (already referenced, kept as-is) or <c>unavailable</c> (an Integration package without a compatible version).</param>
/// <param name="Reason"><c>required</c>, <c>--add &lt;key&gt;</c> or <c>integration</c>.</param>
internal sealed record CloudInitPackage(string Id, string? Version, string Action, string Reason);
