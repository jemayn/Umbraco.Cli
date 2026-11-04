using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Cloud.Secrets;

/// <summary>
/// Pulls the configuration values and secrets for local development from Umbraco Cloud into the <see cref="LocalEnvironmentFile" />
/// at the root of the repository, making sure git ignores it (see ADR 0010).
/// </summary>
internal sealed class LocalSecretsPuller
{
    private readonly ICloudApiClient _cloudApiClient;
    private readonly IGit _git;

    public LocalSecretsPuller(ICloudApiClient cloudApiClient, IGit git)
    {
        _cloudApiClient = cloudApiClient;
        _git = git;
    }

    /// <param name="credentials">The project and CI/CD API key.</param>
    /// <param name="directory">A directory in the repository (the file is written at the root of its git work tree, or in this directory without git).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<SecretsPullResult> PullAsync(CloudCredentials credentials, string directory, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> values = LocalEnvironmentFile.Normalize(await _cloudApiClient.GetLocalDevelopmentValuesAsync(credentials, cancellationToken));

        string? repositoryRoot = await _git.GetRepositoryRootAsync(directory, cancellationToken);
        string root = repositoryRoot ?? Path.GetFullPath(directory);

        // Ignore the file before writing it, so the secrets can't be committed by accident
        bool gitignoreUpdated = repositoryRoot is not null && await EnsureIgnoredAsync(repositoryRoot, cancellationToken);

        string path = Path.Combine(root, LocalEnvironmentFile.FileName);
        LocalEnvironmentFile.Write(path, values);

        return new SecretsPullResult
        {
            File = path,
            Names = values.Keys.Order(StringComparer.Ordinal).ToList(),
            GitignoreUpdated = gitignoreUpdated,
            InGitRepository = repositoryRoot is not null,
        };
    }

    private async Task<bool> EnsureIgnoredAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        if (await _git.IsIgnoredAsync(repositoryRoot, LocalEnvironmentFile.FileName, cancellationToken))
        {
            return false;
        }

        string gitignore = Path.Combine(repositoryRoot, ".gitignore");
        string existing = File.Exists(gitignore) ? File.ReadAllText(gitignore) : string.Empty;
        string separator = existing.Length == 0 ? string.Empty : existing.EndsWith('\n') ? "\n" : "\n\n";
        File.AppendAllText(gitignore, $"{separator}# Local development values pulled from Umbraco Cloud (umbraco cloud secrets pull)\n/{LocalEnvironmentFile.FileName}\n");
        return true;
    }
}
