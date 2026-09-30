using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Cloud;

/// <summary>
/// Defines <c>umbraco cloud deployments upload|deploy|show</c>.
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

        return new Command("cloud", "Work with Umbraco Cloud projects.") { Init.CloudInitCommand.Create(services), deployments };
    }

    private static Command CreateUploadCommand(Func<IServiceProvider> services)
    {
        Option<string> projectId = CreateProjectIdOption();
        Option<string> apiKey = CreateApiKeyOption();
        ArtifactOptions artifact = new();

        var command = new Command("upload", "Upload a zip file or directory as an artifact.") { projectId, apiKey };
        artifact.AddTo(command, required: true);

        command.SetAction((parseResult, cancellationToken) => services()
            .GetRequiredService<UploadCommandHandler>()
            .HandleAsync(new UploadCommandSettings(parseResult.GetValue(projectId), parseResult.GetValue(apiKey), artifact.GetSource(parseResult)), cancellationToken));

        return command;
    }

    private static Command CreateDeployCommand(Func<IServiceProvider> services)
    {
        Option<string> projectId = CreateProjectIdOption();
        Option<string> apiKey = CreateApiKeyOption();
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
            apiKey,
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
        artifact.AddTo(command, required: false);
        command.Validators.Add(result =>
        {
            int sources = (result.GetValue(artifactId) is null ? 0 : 1) + (result.GetValue(artifact.File) is null ? 0 : 1) + (result.GetValue(artifact.Path) is null ? 0 : 1);
            if (sources != 1)
            {
                result.AddError("Specify exactly one of --artifact-id, --file or --path.");
            }
        });

        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new DeployCommandSettings
            {
                ProjectId = parseResult.GetValue(projectId),
                ApiKey = parseResult.GetValue(apiKey),
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
        Option<string> projectId = CreateProjectIdOption();
        Option<string> apiKey = CreateApiKeyOption();
        var deploymentId = new Argument<string>("deployment-id") { Description = "The ID of the deployment." };
        var wait = new Option<bool>("--wait") { Description = "Wait until the deployment has completed or failed." };
        Option<TimeSpan> timeout = CreateTimeoutOption();

        var command = new Command("show", "Show the status of a deployment.") { deploymentId, projectId, apiKey, wait, timeout };
        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new ShowCommandSettings(
                parseResult.GetValue(projectId),
                parseResult.GetValue(apiKey),
                parseResult.GetRequiredValue(deploymentId),
                parseResult.GetValue(wait),
                parseResult.GetValue(timeout));

            return services().GetRequiredService<ShowCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }

    private static Option<string> CreateProjectIdOption() => new("--project-id")
    {
        Description = $"The Umbraco Cloud project ID [env: {CloudCredentialsResolver.ProjectIdVariable}].",
    };

    private static Option<string> CreateApiKeyOption() => new("--api-key")
    {
        Description = $"The Umbraco Cloud CI/CD API key [env: {CloudCredentialsResolver.ApiKeyVariable}].",
    };

    private static Option<TimeSpan> CreateTimeoutOption() => new("--timeout")
    {
        Description = "How long to wait for the deployment, for example 90s, 15m or 1h.",
        DefaultValueFactory = _ => DeploymentWaiter.DefaultTimeout,
        CustomParser = ParseTimeout,
        HelpName = "duration",
    };

    private static TimeSpan ParseTimeout(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return DeploymentWaiter.DefaultTimeout;
        }

        string value = result.Tokens[0].Value;
        if (Durations.TryParse(value, out TimeSpan duration))
        {
            return duration;
        }

        result.AddError($"'{value}' is not a valid duration; use for example 90s, 15m, 1h30m or 00:15:00.");
        return default;
    }

    /// <summary>
    /// The options that describe what to upload.
    /// </summary>
    private sealed class ArtifactOptions
    {
        public Option<string> File { get; } = new("--file", "-f") { Description = "A zip file to upload as-is." };

        public Option<string> Path { get; } = new("--path", "-p") { Description = "A directory to zip and upload (source code for a Git deployment, published output for an Artifact deployment)." };

        public Option<string> Type { get; } = new Option<string>("--type")
        {
            Description = "The deployment type of --path: git (source code) or artifact (published output); detected like Umbraco Cloud does when not specified.",
        }.AcceptOnlyFromAmong("git", "artifact");

        public Option<string> Description { get; } = new("--description") { Description = "A description stored with the artifact." };

        public Option<string> Version { get; } = new("--version") { Description = "A version stored with the artifact." };

        public void AddTo(Command command, bool required)
        {
            command.Options.Add(File);
            command.Options.Add(Path);
            command.Options.Add(Type);
            command.Options.Add(Description);
            command.Options.Add(Version);

            if (required)
            {
                command.Validators.Add(result =>
                {
                    if (result.GetValue(File) is null == result.GetValue(Path) is null)
                    {
                        result.AddError("Specify exactly one of --file or --path.");
                    }
                });
            }

            command.Validators.Add(result =>
            {
                if (result.GetValue(Type) is not null && result.GetValue(Path) is null)
                {
                    result.AddError("--type can only be used with --path.");
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
