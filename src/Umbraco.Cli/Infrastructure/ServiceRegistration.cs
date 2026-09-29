using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Deploy;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Deploy.ProjectDetection;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Registers the services of all commands.
/// </summary>
internal static class ServiceRegistration
{
    public const string ApiUrlVariable = "UMBRACO_CLOUD_API_URL";

    public static void AddCliServices(IServiceCollection services, GlobalSettings settings, IEnvironment environment, TextWriter stdout, TextWriter stderr)
    {
        services.AddSingleton(settings);
        services.AddSingleton(environment);
        services.AddSingleton(TimeProvider.System);

        // Output and progress
        services.AddSingleton<IOutput>(new ConsoleOutput(settings, stdout));
        services.AddSingleton(_ => AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(stderr) }));
        services.AddSingleton<IReporter>(provider => TerminalDetection.UseLiveProgress(settings, environment)
            ? new LiveReporter(
                provider.GetRequiredService<IAnsiConsole>(),
                new TaskbarProgress(stderr, TerminalDetection.SupportsTaskbarProgress(environment)),
                provider.GetRequiredService<TimeProvider>(),
                settings.Verbose)
            : new PlainReporter(stderr, provider.GetRequiredService<TimeProvider>(), settings.Verbose));
        services.AddSingleton<IPrompter, SpectrePrompter>();

        // External tools
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGit, Git>();
        services.AddSingleton<IDotnetCli, DotnetCli>();

        // Umbraco Cloud
        services.AddHttpClient<ICloudApiClient, CloudApiClient>(client =>
        {
            client.BaseAddress = new Uri(environment.GetVariable(ApiUrlVariable) is { Length: > 0 } url ? url.TrimEnd('/') + "/" : CloudApiClient.DefaultBaseAddress);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddSingleton<ArtifactZipBuilder>();
        services.AddTransient<ArtifactUploader>();
        services.AddTransient<DeploymentWaiter>();
        services.AddTransient<UploadCommandHandler>();
        services.AddTransient<DeployCommandHandler>();
        services.AddTransient<ShowCommandHandler>();

        // Umbraco Deploy
        services.AddSingleton<Func<string>>(ApiSecret.Generate);
        services.AddSingleton<Func<Guid>>(Guid.NewGuid);
        services.AddSingleton<IDeployConfigurationWriter, OnPremisesConfigurationWriter>();
        services.AddSingleton<IDeployConfigurationWriter, CloudConfigurationWriter>();
        services.AddSingleton<DeployPackageDetector>();
        services.AddTransient<InitCommandHandler>();
    }
}
