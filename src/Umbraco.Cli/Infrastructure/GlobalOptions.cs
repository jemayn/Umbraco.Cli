using System.CommandLine;
using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The options that apply to every command.
/// </summary>
internal sealed class GlobalOptions
{
    public Option<string> Output { get; } = new Option<string>("--output", "-o")
    {
        Description = "The format of the command result written to stdout.",
        DefaultValueFactory = _ => "text",
        Recursive = true,
    }.AcceptOnlyFromAmong("text", "json");

    public Option<bool> NoProgress { get; } = new("--no-progress")
    {
        Description = "Write progress as plain lines instead of live progress bars and spinners.",
        Recursive = true,
    };

    public Option<bool> Verbose { get; } = new("--verbose")
    {
        Description = "Write HTTP requests and error details to stderr.",
        Recursive = true,
    };

    public void AddTo(Command rootCommand)
    {
        rootCommand.Options.Add(Output);
        rootCommand.Options.Add(NoProgress);
        rootCommand.Options.Add(Verbose);
    }

    public GlobalSettings GetSettings(ParseResult parseResult)
        => parseResult.Errors.Count > 0
            ? new GlobalSettings(OutputFormat.Text, NoProgress: false, Verbose: false)
            : new GlobalSettings(
            parseResult.GetValue(Output) == "json" ? OutputFormat.Json : OutputFormat.Text,
            parseResult.GetValue(NoProgress),
            parseResult.GetValue(Verbose));
}
