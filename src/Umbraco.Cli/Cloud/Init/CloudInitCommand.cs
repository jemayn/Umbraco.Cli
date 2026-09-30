using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Defines <c>umbraco cloud init</c>.
/// </summary>
internal static class CloudInitCommand
{
    public static Command Create(Func<IServiceProvider> services)
    {
        var project = new Option<string>("--project") { Description = "The Umbraco project to prepare: its project file, or a directory containing it (default: the current directory)." };
        var deploymentType = new Option<string>("--deployment-type")
        {
            Description = "How the project is deployed to Umbraco Cloud: artifact (published output, the default) or git (source code, with the .umbraco file and README.md of a Cloud repository).",
            DefaultValueFactory = _ => "artifact",
            HelpName = "artifact|git",
        }.AcceptOnlyFromAmong("artifact", "git");
        var name = new Option<string>("--name", "-n") { Description = $"Create a new Umbraco project with this name first (laid out like the Cloud scaffold)." };
        var output = new Option<string>("--output") { Description = "The repository root to create the new Umbraco project in (default: the current directory)." };
        var version = new Option<string>("--version") { Description = "The Umbraco version of the new project: latest (default), lts, rc, beta, next, a major (17), a major.minor (17.2) or an exact version.", HelpName = "version" };
        var developmentMode = new Option<string>("--development-mode")
        {
            Description = "How the new project is developed: ide (default: Production runtime mode, models as source code) or backoffice (as in the Cloud scaffold).",
            HelpName = "ide|backoffice",
        }.AcceptOnlyFromAmong("ide", "backoffice");
        var excludeGitignore = new Option<bool>("--exclude-gitignore") { Description = "Don't create .gitignore files (the .NET one at the repository root and the template's Umbraco-specific one in the project)." };
        var add = new Option<string[]>("--add") { Description = "A Product to add, such as forms (repeatable).", AllowMultipleArgumentsPerToken = false, HelpName = "product" };
        var identityClientId = new Option<string>("--identity-client-id") { Description = $"The Umbraco ID client ID for umbraco-cloud.json [env: {IdentityValues.ClientIdVariable}]." };
        var identityClientSecret = new Option<string>("--identity-client-secret") { Description = $"The Umbraco ID client secret for umbraco-cloud.json [env: {IdentityValues.ClientSecretVariable}]." };
        var allowEndOfLife = new Option<bool>("--allow-end-of-life") { Description = "Allow creating a project on an end-of-life Umbraco version." };
        var noBuild = new Option<bool>("--no-build") { Description = "Restore the project, but don't build it." };
        var nonInteractive = new Option<bool>("--non-interactive") { Description = "Never prompt; use defaults or fail when a required value is missing." };
        var templateOptions = new Argument<string[]>("template-options")
        {
            Description = "Options passed to 'dotnet new umbraco' after --, e.g. -- --development-database-type SQLite.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        var command = new Command("init", "Prepare an Umbraco project for Umbraco Cloud, creating it first when --name is given.")
        {
            project,
            deploymentType,
            name,
            output,
            version,
            developmentMode,
            excludeGitignore,
            add,
            identityClientId,
            identityClientSecret,
            allowEndOfLife,
            noBuild,
            nonInteractive,
            templateOptions,
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            var settings = new CloudInitSettings
            {
                Project = parseResult.GetValue(project),
                DeploymentType = parseResult.GetValue(deploymentType) ?? "artifact",
                Name = parseResult.GetValue(name),
                Output = parseResult.GetValue(output),
                Version = parseResult.GetValue(version),
                DevelopmentMode = parseResult.GetValue(developmentMode),
                ExcludeGitignore = parseResult.GetValue(excludeGitignore),
                Add = parseResult.GetValue(add) ?? [],
                IdentityClientId = parseResult.GetValue(identityClientId),
                IdentityClientSecret = parseResult.GetValue(identityClientSecret),
                AllowEndOfLife = parseResult.GetValue(allowEndOfLife),
                NoBuild = parseResult.GetValue(noBuild),
                NonInteractive = parseResult.GetValue(nonInteractive),
                TemplateOptions = parseResult.GetValue(templateOptions) ?? [],
            };

            return services().GetRequiredService<CloudInitCommandHandler>().HandleAsync(settings, cancellationToken);
        });

        return command;
    }
}
