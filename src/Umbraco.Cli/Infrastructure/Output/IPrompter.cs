namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Asks the user for input (only used when stdin and stderr are interactive).
/// </summary>
internal interface IPrompter
{
    Task<string> SelectAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken);
}
