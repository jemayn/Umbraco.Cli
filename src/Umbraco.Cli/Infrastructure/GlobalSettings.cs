using Umbraco.Cli.Infrastructure.Output;

namespace Umbraco.Cli.Infrastructure;

/// <summary>
/// The values of the options that apply to every command.
/// </summary>
internal sealed record GlobalSettings(OutputFormat Output, bool NoProgress, bool Verbose);
