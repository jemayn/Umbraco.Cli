using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Maps exceptions that escape a command to an error message and exit code.
/// </summary>
internal static class ErrorHandler
{
    public static int Handle(Exception exception, IReporter reporter, bool verbose)
    {
        switch (exception)
        {
            case OperationCanceledException:
                reporter.Error("Cancelled.");
                return ExitCodes.Cancelled;

            case CliException cliException:
                reporter.Error(cliException.Message, cliException.Hint);
                WriteDetails(cliException.InnerException, reporter, verbose);
                return cliException.ExitCode;

            case HttpRequestException httpRequestException:
                reporter.Error($"Could not reach Umbraco Cloud: {httpRequestException.Message}", verbose ? null : "Run with --verbose for details.");
                WriteDetails(httpRequestException, reporter, verbose);
                return ExitCodes.Error;

            default:
                reporter.Error(exception.Message, verbose ? null : "Run with --verbose for details.");
                WriteDetails(exception, reporter, verbose);
                return ExitCodes.Error;
        }
    }

    private static void WriteDetails(Exception? exception, IReporter reporter, bool verbose)
    {
        if (verbose && exception is not null)
        {
            reporter.Verbose(exception.ToString());
        }
    }
}
