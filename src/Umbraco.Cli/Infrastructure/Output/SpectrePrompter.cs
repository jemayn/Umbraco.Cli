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
        => new SelectionPrompt<string>().Title(Markup.Escape(title)).AddChoices(choices).UseConverter(Markup.Escape).ShowAsync(_console, cancellationToken);

    public async Task<IReadOnlyList<string>> SelectManyAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken)
        => await new MultiSelectionPrompt<string>()
            .Title(Markup.Escape(title))
            .NotRequired()
            .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to accept)[/]")
            .AddChoices(choices)
            .UseConverter(Markup.Escape)
            .ShowAsync(_console, cancellationToken);

    public Task<string> AskAsync(string question, string defaultValue, CancellationToken cancellationToken)
        => new TextPrompt<string>(Markup.Escape(question)).DefaultValue(defaultValue).ShowAsync(_console, cancellationToken);
}
