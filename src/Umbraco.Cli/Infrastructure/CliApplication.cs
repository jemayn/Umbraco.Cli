using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Deploy;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Parses the command line, composes the services for the invoked command and maps its outcome to an exit code.
/// </summary>
internal sealed class CliApplication
{
    private readonly IEnvironment _environment;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly GlobalOptions _globalOptions = new();
    private IServiceProvider? _services;

    public CliApplication(IEnvironment environment, TextWriter stdout, TextWriter stderr, Action<IServiceCollection>? configureServices = null)
    {
        _environment = environment;
        _stdout = stdout;
        _stderr = stderr;
        _configureServices = configureServices;
    }

    /// <summary>
    /// The program name shown in help: the tool command, not the assembly name that <see cref="RootCommand" /> would use.
    /// </summary>
    public const string ProgramName = "umbraco";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Command rootCommand = CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(args);
        GlobalSettings settings = _globalOptions.GetSettings(parseResult);

        var serviceCollection = new ServiceCollection();
        ServiceRegistration.AddCliServices(serviceCollection, settings, _environment, _stdout, _stderr);
        _configureServices?.Invoke(serviceCollection);

        await using ServiceProvider services = serviceCollection.BuildServiceProvider();
        _services = services;
        IReporter reporter = services.GetRequiredService<IReporter>();

        if (parseResult.Errors.Count > 0)
        {
            foreach (System.CommandLine.Parsing.ParseError error in parseResult.Errors)
            {
                reporter.Error(error.Message);
            }

            reporter.Info("Run with --help for usage.");
            return ExitCodes.Usage;
        }

        try
        {
            var configuration = new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false,
                Output = _stdout,
                Error = _stderr,

                // Ctrl+C is handled by Program, so commands can report that a remote operation continues
                ProcessTerminationTimeout = null,
            };

            return await parseResult.InvokeAsync(configuration, cancellationToken);
        }
        catch (Exception exception)
        {
            return ErrorHandler.Handle(exception, reporter, settings.Verbose);
        }
    }

    internal Command CreateRootCommand()
    {
        var rootCommand = new Command(ProgramName, "Command-line tool for Umbraco products and services in development and CI pipelines.")
        {
            new HelpOption(),
            new VersionOption(),
        };
        _globalOptions.AddTo(rootCommand);
        rootCommand.Subcommands.Add(CloudCommands.Create(GetServices));
        rootCommand.Subcommands.Add(DeployCommands.Create(GetServices));
        return rootCommand;
    }

    private IServiceProvider GetServices()
        => _services ?? throw new InvalidOperationException("Services are only available while a command is invoked.");
}
