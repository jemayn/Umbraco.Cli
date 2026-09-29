using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli;

/// <summary>
/// The entry point of the <c>umbraco</c> command.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // The first Ctrl+C lets the running command stop gracefully (and report that a remote operation continues);
            // a second one terminates the process, in case something doesn't observe cancellation
            if (cancellation.IsCancellationRequested is false)
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            }
        };

        var application = new CliApplication(new SystemEnvironment(), Console.Out, Console.Error);
        return await application.RunAsync(args, cancellation.Token);
    }
}
