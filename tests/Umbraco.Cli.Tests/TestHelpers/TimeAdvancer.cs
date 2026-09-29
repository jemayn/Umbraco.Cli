using Microsoft.Extensions.Time.Testing;

namespace Umbraco.Cli.Tests.TestHelpers;

/// <summary>
/// Completes a task that waits on a <see cref="FakeTimeProvider" /> by repeatedly advancing its time.
/// </summary>
internal static class TimeAdvancer
{
    public static async Task<T> RunAsync<T>(Task<T> task, FakeTimeProvider timeProvider, TimeSpan step, int maxSteps = 1000)
    {
        for (int i = 0; i < maxSteps && task.IsCompleted is false; i++)
        {
            // Let the task reach its next delay before moving the time on
            await Task.Delay(5);
            timeProvider.Advance(step);
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
