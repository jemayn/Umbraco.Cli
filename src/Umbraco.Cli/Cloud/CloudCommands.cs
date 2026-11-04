using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Defines <c>umbraco cloud deployments upload|deploy|show</c> (and adds <c>umbraco cloud init</c> and <c>umbraco cloud secrets</c>).
/// </summary>
internal static class CloudCommands
{
    public static Command Create(Func<IServiceProvider> services)
    {
        var deployments = new Command("deployments", "Upload artifacts to Umbraco Cloud and deploy them to environments.")
        {
            CreateUploadCommand(services),
            CreateDeployCommand(services),
            CreateShowCommand(services),
        };

        return new Command("cloud", "Work with Umbraco Cloud projects.") { Init.CloudInitCommand.Create(services), deployments, Secrets.SecretsCommands.Create(services) };
    }

    private static Command CreateUploadCommand(Func<IServiceProvider> services)
    {
        Option<string> projectId = CreateProjectIdOption(fromPath: true);
        Option<string> config = CreateConfigOption(fromPath: true);
        ArtifactOptions artifact = new();

        var command = new Command("upload", "Upload a zip file or directory as an artifact.") { projectId, config };
        artifact.AddTo(command);
        command.Validators.Add(result =>
        {
            if (result.GetValue(artifact.File) is not null && result.GetValue(artifact.Path) is not null)
            {
                result.AddError("Specify only one of --file or --path.");
            }
        });

        command.SetAction((parseResult, cancellationToken) => services()
            .GetRequiredService<UploadCommandHandler>()
            .HandleAsync(new UploadCommandSettings(parseResult.GetValue(projectId), artifact.GetSource(parseResult), parseResult.GetValue(config)), cancellationToken));

        return command;
    }

    private static Command CreateDeployCommand(Func<IServiceProvider> services)
    {
        Option<string> projectId = CreateProjectIdOption(fromPath: true);
        Option<string> config = CreateConfigOption(fromPath: true);
        var artifactId = new Option<string>("--artifact-id") { Description = "The ID of an uploaded artifact to deploy." };
        ArtifactOptions artifact = new();
        var environment = new Option<string>("--environment", "-e")
        {
            Description = "The alias of the target environment (the left-most or a flexible environment, or any environment when the Cloud project allows deploying to any target, as artifact deployments do).",
            Required = true,
        };
        var message = new Option<string>("--message", "-m") { Description = "The commit message (defaults to the subject of the current git HEAD commit)." };
        var noBuildAndRestore = new Option<bool>("--no-build-and-restore") { Description = "Skip the restore and build in the isolated instance before deploying." };
        var skipVersionCheck = new Option<bool>("--skip-version-check") { Description = "Allow deploying older package versions than the environment has." };
        var skipPreserve = new Option<bool>("--skip-preserve-umbraco-cloud-json") { Description = "Overwrite umbraco-cloud.json on Umbraco Cloud with the one in the artifact." };
        var noSchemaExtraction = new Option<bool>("--no-schema-extraction") { Description = "Don't run a schema extraction after the deployment." };
        var noWait = new Option<bool>("--no-wait") { Description = "Return as soon as the deployment has started." };
        Option<TimeSpan> timeout = CreateTimeoutOption();

        var command = new Command("deploy", "Deploy an artifact to an environment, uploading it first when a file or directory is given.")
        {
            projectId,
            config,
            artifactId,
            environment,
            message,
            noBuildAndRestore,
            skipVersionCheck,
            skipPreserve,
            noSchemaExtraction,
            noWait,
            timeout,
        };
        artifact.AddTo(command);
        command.Validators.Add(result =>
        {
            int sources = (result.GetValue(artifactId) is null ? 0 : 1) + (result.GetValue(artifact.File) is null ? 0 : 1) + (result.GetValue(artifact.Path) is null ? 0 : 1);
            if (sources > 1)
            {
                result.AddError("Specify only one of --artifact-id, --file or --path.");
            }
            else if (result.GetValue(artifactId) is not null && result.GetValue(artifact.Type) is not null)
            {
                result.AddError("--type can't be used with --artifact-id.");
            }
        });

        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new DeployCommandSettings
            {
                ProjectId = parseResult.GetValue(projectId),
                Config = parseResult.GetValue(config),
                ArtifactId = parseResult.GetValue(artifactId),
                Source = parseResult.GetValue(artifactId) is null ? artifact.GetSource(parseResult) : null,
                Environment = parseResult.GetRequiredValue(environment),
                Message = parseResult.GetValue(message),
                NoBuildAndRestore = parseResult.GetValue(noBuildAndRestore),
                SkipVersionCheck = parseResult.GetValue(skipVersionCheck),
                SkipPreserveUmbracoCloudJson = parseResult.GetValue(skipPreserve),
                NoSchemaExtraction = parseResult.GetValue(noSchemaExtraction),
                NoWait = parseResult.GetValue(noWait),
                Timeout = parseResult.GetValue(timeout),
            };

            return services().GetRequiredService<DeployCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }

    private static Command CreateShowCommand(Func<IServiceProvider> services)
    {
        Option<string> projectId = CreateProjectIdOption(fromPath: false);
        Option<string> config = CreateConfigOption(fromPath: false);
        var deploymentId = new Argument<string>("deployment-id") { Description = "The ID of the deployment (default: the latest one).", Arity = ArgumentArity.ZeroOrOne };
        var environment = new Option<string>("--environment", "-e") { Description = "Show the latest deployment to the environment with this alias, instead of a deployment ID." };
        var wait = new Option<bool>("--wait") { Description = "Wait until the deployment has completed or failed." };
        Option<TimeSpan> timeout = CreateTimeoutOption();

        var command = new Command("show", "Show the status of a deployment.") { deploymentId, projectId, config, environment, wait, timeout };
        command.Validators.Add(result =>
        {
            if (result.GetValue(deploymentId) is not null && result.GetValue(environment) is not null)
            {
                result.AddError("Specify only one of a deployment ID or --environment.");
            }
        });

        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new ShowCommandSettings(
                parseResult.GetValue(projectId),
                parseResult.GetValue(deploymentId),
                parseResult.GetValue(wait),
                parseResult.GetValue(timeout),
                parseResult.GetValue(config),
                parseResult.GetValue(environment));

            return services().GetRequiredService<ShowCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }

    private static Option<string> CreateProjectIdOption(bool fromPath) => new("--project-id")
    {
        Description = fromPath
            ? $"The Umbraco Cloud project ID [env: {CloudCredentialsResolver.ProjectIdVariable}] (default: Deploy:Project:Id in the umbraco-cloud.json of --config, --path or the Umbraco project in the repository)."
            : $"The Umbraco Cloud project ID [env: {CloudCredentialsResolver.ProjectIdVariable}] (default: Deploy:Project:Id in the umbraco-cloud.json of --config or the Umbraco project in the repository).",
    };

    private static Option<string> CreateConfigOption(bool fromPath) => new("--config")
    {
        Description = fromPath
            ? "The umbraco-cloud.json to read the project ID from, or a folder containing it (default: the one in --path, else the one of the Umbraco project in the repository)."
            : "The umbraco-cloud.json to read the project ID from, or a folder containing it (default: the one of the Umbraco project in the repository).",
    };

    private static Option<TimeSpan> CreateTimeoutOption()
        => TimeoutOption.Create("How long to wait for the deployment, for example 90s, 15m or 1h.", DeploymentWaiter.DefaultTimeout);

    /// <summary>
    /// The options that describe what to upload.
    /// </summary>
    private sealed class ArtifactOptions
    {
        public Option<string> File { get; } = new("--file", "-f") { Description = "A zip file to upload as-is." };

        public Option<string> Path { get; } = new("--path", "-p")
        {
            Description = "A directory to zip and upload: source code for a Git deployment, published output for an Artifact deployment (default: the repository, when it has a .umbraco file).",
        };

        public Option<string> Type { get; } = new Option<string>("--type")
        {
            Description = "The deployment type of the directory: git (source code) or artifact (published output); detected like Umbraco Cloud does when not specified.",
        }.AcceptOnlyFromAmong("git", "artifact");

        public Option<string> Description { get; } = new("--description") { Description = "A description stored with the artifact." };

        public Option<string> Version { get; } = new("--version") { Description = "A version stored with the artifact." };

        public void AddTo(Command command)
        {
            command.Options.Add(File);
            command.Options.Add(Path);
            command.Options.Add(Type);
            command.Options.Add(Description);
            command.Options.Add(Version);

            command.Validators.Add(result =>
            {
                if (result.GetValue(Type) is not null && result.GetValue(File) is not null)
                {
                    result.AddError("--type can't be used with --file.");
                }
            });
        }

        public ArtifactSource GetSource(ParseResult parseResult)
            => new(
                parseResult.GetValue(File),
                parseResult.GetValue(Path),
                parseResult.GetValue(Type) switch
                {
                    "git" => DeploymentType.Git,
                    "artifact" => DeploymentType.Artifact,
                    _ => null,
                },
                parseResult.GetValue(Description),
                parseResult.GetValue(Version));
    }
}
