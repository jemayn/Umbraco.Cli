namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Asks the user for input (only used when stdin and stderr are interactive).
/// </summary>
internal interface IPrompter
{
    Task<string> SelectAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> SelectManyAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken);

    Task<string> AskAsync(string question, string defaultValue, CancellationToken cancellationToken);
}
