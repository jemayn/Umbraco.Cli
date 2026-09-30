namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// A package <c>umbraco cloud init</c> installs, or found already referenced.
/// </summary>
/// <param name="Id">The package ID.</param>
/// <param name="Reason">Why it's installed: <c>required</c>, <c>--add &lt;key&gt;</c> or <c>integration</c>.</param>
/// <param name="ExistingVersion">The version already referenced by the Umbraco project, if any (then it's kept as-is).</param>
internal sealed record PlannedPackage(string Id, string Reason, string? ExistingVersion)
{
    public const string RequiredReason = "required";

    public const string IntegrationReason = "integration";

    public bool IsReferenced => ExistingVersion is not null;

    public bool IsIntegration => Reason == IntegrationReason;
}
