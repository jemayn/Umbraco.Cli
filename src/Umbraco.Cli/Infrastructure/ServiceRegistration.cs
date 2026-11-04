using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Umbraco.Cli.Cloud;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Cloud.Init;
using Umbraco.Cli.Cloud.Secrets;
using Umbraco.Cli.Deploy;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Deploy.ProjectDetection;
using Umbraco.Cli.Deploy.Schema;
using Umbraco.Cli.Deploy.Schema.Api;
using Umbraco.Cli.Infrastructure.NuGet;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Releases;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// Registers the services of all commands.
/// </summary>
internal static class ServiceRegistration
{
    public const string ApiUrlVariable = "UMBRACO_CLOUD_API_URL";

    public static void AddCliServices(IServiceCollection services, GlobalSettings settings, IEnvironment environment, TextWriter stdout, TextWriter stderr)
    {
        // Environment variables fall back to the values pulled from Umbraco Cloud into the repository's .env.umbraco
        environment = new LocalFileEnvironment(environment);
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
            client.BaseAddress = CloudApiClient.GetBaseAddress(environment.GetVariable(ApiUrlVariable), ApiUrlVariable);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddSingleton<ArtifactZipBuilder>();
        services.AddTransient<ArtifactUploader>();
        services.AddTransient<DeploymentWaiter>();
        services.AddTransient<UploadCommandHandler>();
        services.AddTransient<DeployCommandHandler>();
        services.AddTransient<ShowCommandHandler>();

        // NuGet and Umbraco releases (the registration API is served gzip-compressed)
        services.AddHttpClient<INuGetClient, NuGetClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
        services.AddSingleton<UmbracoReleases>();
        services.AddSingleton<PackageVersionResolver>();

        // umbraco cloud init
        services.AddTransient<UmbracoProjectCreator>();
        services.AddTransient<CloudInitCommandHandler>();

        // umbraco cloud secrets
        services.AddTransient<LocalSecretsPuller>();
        services.AddTransient<SecretsPullCommandHandler>();

        // Umbraco Deploy (secrets are only generated for Umbraco Deploy On-Premises; Umbraco Cloud provisions its own)
        services.AddSingleton<Func<string>>(ApiSecret.Generate);
        services.AddSingleton<Func<Guid>>(Guid.NewGuid);
        services.AddSingleton<IDeployConfigurationWriter, OnPremisesConfigurationWriter>();
        services.AddSingleton<IDeployConfigurationWriter, CloudConfigurationWriter>();
        services.AddSingleton<DeployPackageDetector>();
        services.AddTransient<InitCommandHandler>();

        // Umbraco Deploy schema deployments (the base URL differs per Environment, so requests use absolute URIs)
        services.AddHttpClient<IDeployApiClient, DeployApiClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<SchemaDeploymentWaiter>();
        services.AddTransient<SchemaDeployCommandHandler>();
        services.AddTransient<SchemaShowCommandHandler>();
    }
}
