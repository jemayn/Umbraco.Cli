namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The process exit codes returned by the CLI.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;

    public const int Error = 1;

    public const int Usage = 2;

    public const int DeploymentFailed = 3;

    public const int Timeout = 4;

    public const int AuthenticationFailed = 5;

    public const int Cancelled = 130;
}
