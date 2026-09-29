using Spectre.Console;

namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Prompts on stderr using Spectre.Console.
/// </summary>
internal sealed class SpectrePrompter : IPrompter
{
    private readonly IAnsiConsole _console;

    public SpectrePrompter(IAnsiConsole console) => _console = console;

    public Task<string> SelectAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken)
        => new SelectionPrompt<string>().Title(Markup.Escape(title)).AddChoices(choices).ShowAsync(_console, cancellationToken);
}
