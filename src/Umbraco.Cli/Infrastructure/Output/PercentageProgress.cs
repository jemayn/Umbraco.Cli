namespace Umbraco.Cli.Infrastructure.Output;

/// <summary>
/// Converts byte progress into callbacks at every 10% boundary (synchronously, unlike <see cref="Progress{T}" />).
/// </summary>
internal sealed class PercentageProgress : IProgress<long>
{
    private readonly long _total;
    private readonly Action<int> _onBoundary;
    private int _lastReported = -1;

    public PercentageProgress(long total, Action<int> onBoundary)
    {
        _total = total;
        _onBoundary = onBoundary;
    }

    public void Report(long value)
    {
        int percentage = _total <= 0 ? 100 : (int)Math.Clamp(value * 100 / _total, 0, 100);
        int boundary = percentage / 10 * 10;
        if (boundary > _lastReported && boundary > 0)
        {
            _lastReported = boundary;
            _onBoundary(boundary);
        }
    }
}
