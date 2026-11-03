using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Cli.Cloud.Secrets;

/// <summary>
/// Defines <c>umbraco cloud secrets pull</c>.
/// </summary>
internal static class SecretsCommands
{
    public static Command Create(Func<IServiceProvider> services)
    {
        var projectId = new Option<string>("--project-id")
        {
            Description = $"The Umbraco Cloud project ID [env: {CloudCredentialsResolver.ProjectIdVariable}] (default: Deploy:Project:Id in the umbraco-cloud.json of the current directory).",
        };

        var pull = new Command(
            "pull",
            $"Pull the configuration values and secrets for local development from Umbraco Cloud into {Infrastructure.LocalEnvironmentFile.FileName} at the root of the repository (kept out of git). Needs the {CloudCredentialsResolver.ApiKeyVariable} environment variable.")
        {
            projectId,
        };
        pull.SetAction((parseResult, cancellationToken) => services().GetRequiredService<SecretsPullCommandHandler>().HandleAsync(parseResult.GetValue(projectId), cancellationToken));

        return new Command("secrets", "Work with the configuration values and secrets of an Umbraco Cloud project for local development.") { pull };
    }
}
