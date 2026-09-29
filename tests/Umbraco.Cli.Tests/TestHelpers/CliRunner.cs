using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// Runs the CLI in-process with captured stdout and stderr.
/// </summary>
internal static class CliRunner
{
    public static async Task<CliResult> RunAsync(IEnvironment environment, Action<IServiceCollection>? configureServices, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var application = new CliApplication(environment, stdout, stderr, configureServices);

        int exitCode = await application.RunAsync(args);
        return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    public static Task<CliResult> RunAsync(params string[] args) => RunAsync(new FakeEnvironment(), null, args);
}
