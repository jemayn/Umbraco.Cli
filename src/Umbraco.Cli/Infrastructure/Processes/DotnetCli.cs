namespace Umbraco.Cli.Infrastructure.Processes;

/// <inheritdoc />
internal sealed class DotnetCli : IDotnetCli
{
    private readonly IProcessRunner _processRunner;

    public DotnetCli(IProcessRunner processRunner) => _processRunner = processRunner;

    public async Task<string> GetSdkVersionAsync(string directory, CancellationToken cancellationToken)
    {
        ProcessResult result = await _processRunner.RunAsync("dotnet", ["--version"], directory, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new CliException(
                $"Could not determine the .NET SDK version: {FirstNonEmpty(result.StandardError, result.StandardOutput)}",
                ExitCodes.Error,
                "Install the .NET SDK 10 or later (and check any global.json that pins an SDK version).");
        }

        return result.StandardOutput.Trim();
    }

    public async Task<string> ListPackagesAsync(string projectPath, bool interactive, CancellationToken cancellationToken)
    {
        List<string> arguments = ["package", "list", "--project", projectPath, "--include-transitive", "--format", "json"];
        if (interactive)
        {
            arguments.Add("--interactive");
        }

        ProcessResult result = await _processRunner.RunAsync("dotnet", arguments, Path.GetDirectoryName(projectPath)!, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new CliException($"Could not list the packages of '{projectPath}': {FirstNonEmpty(result.StandardError, result.StandardOutput)}");
        }

        return result.StandardOutput;
    }

    private static string FirstNonEmpty(params string[] values)
        => values.Select(value => value.Trim()).FirstOrDefault(value => value.Length > 0) ?? "no output";
}
