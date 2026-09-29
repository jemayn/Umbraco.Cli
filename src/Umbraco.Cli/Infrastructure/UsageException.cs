namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Invalid usage or input that the user can fix by changing the command line (exit code 2).
/// </summary>
internal sealed class UsageException : CliException
{
    public UsageException(string message, string? hint = null)
        : base(message, ExitCodes.Usage, hint)
    {
    }
}
