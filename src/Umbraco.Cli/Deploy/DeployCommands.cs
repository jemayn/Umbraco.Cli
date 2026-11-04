using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cli.Deploy.Configuration;

namespace Umbraco.Cli.Deploy;

/// <summary>
/// Defines <c>umbraco deploy init</c> and <c>umbraco deploy schema deploy|show</c>.
/// </summary>
internal static class DeployCommands
{
    public static Command Create(Func<IServiceProvider> services)
        => new("deploy", "Configure Umbraco Deploy and run schema deployments.") { CreateInitCommand(services), Schema.SchemaCommands.Create(services) };

    private static Command CreateInitCommand(Func<IServiceProvider> services)
    {
        var project = new Option<string>("--project") { Description = "The site's project file, or a directory containing it (default: the current directory)." };
        var environments = new Option<string[]>("--environment", "-e")
        {
            Description = $"An environment to configure, as \"{EnvironmentSpec.Format}\" (repeat for each environment; replaces the configured list).",
            AllowMultipleArgumentsPerToken = false,
        };
        var currentEnvironment = new Option<string>("--current-environment") { Description = "The name of the environment this site is (Umbraco Deploy On-Premises only)." };
        var projectId = new Option<string>("--project-id") { Description = $"The Umbraco Cloud project ID, set if not configured yet (Umbraco Deploy Cloud only) [env: {Cloud.CloudCredentialsResolver.ProjectIdVariable}]." };
        var rotateSecret = new Option<bool>("--rotate-secret") { Description = "Replace an existing ApiSecret or ApiKey with a new ApiSecret (Umbraco Deploy On-Premises only)." };
        var baselineUrl = new Option<string>("--baseline-url") { Description = $"The URL of the Baseline project's Live environment, for a Child project to restore from, with the Baseline secret from the {InitCommandHandler.BaselineSecretVariable} environment variable (Umbraco Deploy On-Premises only)." };
        var rotateBaselineSecret = new Option<bool>("--rotate-baseline-secret") { Description = "Generate a new Baseline secret for this Child project; set it as a child secret on the Baseline project." };
        var removeBaseline = new Option<bool>("--remove-baseline") { Description = "Remove the Baseline URL and Baseline secret of this Child project." };
        var childSecret = new Option<string>("--child-secret") { Description = "Generate the child secret with this name for a Child project of this Baseline project; a valid existing one is kept." };
        var removeChildSecret = new Option<string>("--remove-child-secret") { Description = "Remove the child secret with this name from this Baseline project." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Show the changes without writing them." };
        var nonInteractive = new Option<bool>("--non-interactive") { Description = "Never prompt; fail when a required value is missing." };

        var command = new Command("init", "Ensure an ApiSecret is configured and environments are added for Umbraco Deploy.")
        {
            project,
            environments,
            currentEnvironment,
            projectId,
            rotateSecret,
            baselineUrl,
            rotateBaselineSecret,
            removeBaseline,
            childSecret,
            removeChildSecret,
            dryRun,
            nonInteractive,
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new InitCommandSettings
            {
                Project = parseResult.GetValue(project),
                Environments = parseResult.GetValue(environments) ?? [],
                CurrentEnvironment = parseResult.GetValue(currentEnvironment),
                ProjectId = parseResult.GetValue(projectId),
                RotateSecret = parseResult.GetValue(rotateSecret),
                BaselineUrl = parseResult.GetValue(baselineUrl),
                RotateBaselineSecret = parseResult.GetValue(rotateBaselineSecret),
                RemoveBaseline = parseResult.GetValue(removeBaseline),
                ChildSecret = parseResult.GetValue(childSecret),
                RemoveChildSecret = parseResult.GetValue(removeChildSecret),
                DryRun = parseResult.GetValue(dryRun),
                NonInteractive = parseResult.GetValue(nonInteractive),
            };

            return services().GetRequiredService<InitCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }
}
