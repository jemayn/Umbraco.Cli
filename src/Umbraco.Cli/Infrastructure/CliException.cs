namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// An expected failure that is reported to the user as a single error line (plus an optional hint) and mapped to an exit code.
/// </summary>
internal class CliException : Exception
{
    public CliException(string message, int exitCode = ExitCodes.Error, string? hint = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
        Hint = hint;
    }

    public int ExitCode { get; }

    public string? Hint { get; }
}
