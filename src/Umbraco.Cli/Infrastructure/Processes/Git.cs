namespace Umbraco.Cli.Infrastructure.Processes;

/// <inheritdoc />
internal sealed class Git : IGit
{
    private readonly IProcessRunner _processRunner;

    public Git(IProcessRunner processRunner) => _processRunner = processRunner;

    public async Task<IReadOnlyList<string>> ListFilesAsync(string directory, CancellationToken cancellationToken)
    {
        ProcessResult result = await _processRunner.RunAsync(
            "git",
            ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
            directory,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new CliException(
                $"Could not list the files to upload with git: {result.StandardError.Trim()}",
                ExitCodes.Error,
                "A Git deployment is created from a git work tree; use --type artifact to upload all files instead.");
        }

        return result.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<string?> GetHeadSubjectAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            ProcessResult result = await _processRunner.RunAsync("git", ["log", "-1", "--format=%s"], directory, cancellationToken);
            string subject = result.StandardOutput.Trim();
            return result.ExitCode == 0 && subject.Length > 0 ? subject : null;
        }
        catch (CliException)
        {
            // git is not installed
            return null;
        }
    }
}
