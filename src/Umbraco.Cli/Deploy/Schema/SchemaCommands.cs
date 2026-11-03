using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cli.Infrastructure;

namespace Umbraco.Cli.Deploy.Schema;

/// <summary>
/// Defines <c>umbraco deploy schema deploy|show</c>.
/// </summary>
internal static class SchemaCommands
{
    public const string DefaultReason = "Umbraco CLI";

    private const string ReasonPunctuation = " -_.~";

    public static Command Create(Func<IServiceProvider> services)
        => new("schema", "Run schema deployments: make an Environment update its schema from the deployed Umbraco Deploy schema files.")
        {
            CreateDeployCommand(services),
            CreateShowCommand(services),
        };

    private static Command CreateDeployCommand(Func<IServiceProvider> services)
    {
        var target = new TargetOptions();
        var reason = new Option<string>("--reason")
        {
            Description = "Why the schema deployment runs, as logged by Umbraco Deploy (letters, digits, spaces and -_.~).",
            DefaultValueFactory = _ => DefaultReason,
        };
        var noWait = new Option<bool>("--no-wait") { Description = "Return as soon as the schema deployment has started." };
        Option<TimeSpan> timeout = CreateTimeoutOption();

        var command = new Command("deploy", "Start a schema deployment on an Environment and wait for it.");
        target.AddTo(command);
        command.Options.Add(reason);
        command.Options.Add(noWait);
        command.Options.Add(timeout);
        command.Validators.Add(result =>
        {
            // Validators don't see the default value
            string value = result.GetValue(reason) ?? DefaultReason;
            if (string.IsNullOrWhiteSpace(value) || value.Any(character => char.IsAsciiLetterOrDigit(character) is false && ReasonPunctuation.Contains(character, StringComparison.Ordinal) is false))
            {
                result.AddError("--reason must only contain letters, digits, spaces and -_.~ (and can't be empty).");
            }
        });

        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new SchemaDeploySettings
            {
                Target = target.GetSettings(parseResult),
                Reason = parseResult.GetRequiredValue(reason).Trim(),
                NoWait = parseResult.GetValue(noWait),
                Timeout = parseResult.GetValue(timeout),
            };

            return services().GetRequiredService<SchemaDeployCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }

    private static Command CreateShowCommand(Func<IServiceProvider> services)
    {
        var target = new TargetOptions();
        var taskId = new Argument<Guid?>("task-id") { Description = "The task ID of the schema deployment (default: the current or last one).", Arity = ArgumentArity.ZeroOrOne };
        var wait = new Option<bool>("--wait") { Description = "Wait until the schema deployment has completed or failed." };
        Option<TimeSpan> timeout = CreateTimeoutOption();

        var command = new Command("show", "Show the status of a schema deployment.") { taskId };
        target.AddTo(command);
        command.Options.Add(wait);
        command.Options.Add(timeout);
        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new SchemaShowSettings
            {
                Target = target.GetSettings(parseResult),
                TaskId = parseResult.GetValue(taskId),
                Wait = parseResult.GetValue(wait),
                Timeout = parseResult.GetValue(timeout),
            };

            return services().GetRequiredService<SchemaShowCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }

    private static Option<TimeSpan> CreateTimeoutOption()
        => TimeoutOption.Create("How long to wait for the schema deployment, for example 90s, 15m or 1h.", SchemaDeploymentWaiter.DefaultTimeout);

    /// <summary>
    /// The options that select the Environment and its credentials.
    /// </summary>
    private sealed class TargetOptions
    {
        public Option<string> Environment { get; } = new("--environment", "-e")
        {
            Description = "The name of the target Environment, as configured for Umbraco Deploy in --config.",
        };

        public Option<string> Url { get; } = new("--url") { Description = "The URL of the target Environment, instead of --environment." };

        public Option<string> Config { get; } = new("--config")
        {
            Description = $"The {SchemaTargetResolver.AppSettingsFileName} or {SchemaTargetResolver.UmbracoCloudFileName} of the Umbraco project to read the Environments and the ApiSecret from, or a folder containing them, such as the published output (default: the current directory).",
        };

        public void AddTo(Command command)
        {
            command.Options.Add(Environment);
            command.Options.Add(Url);
            command.Options.Add(Config);

            command.Validators.Add(result =>
            {
                if ((result.GetValue(Environment) is null) == (result.GetValue(Url) is null))
                {
                    result.AddError("Specify exactly one of --environment or --url.");
                }
            });
        }

        public SchemaTargetSettings GetSettings(ParseResult parseResult)
            => new()
            {
                Config = parseResult.GetValue(Config),
                Environment = parseResult.GetValue(Environment),
                Url = parseResult.GetValue(Url),
            };
    }
}
