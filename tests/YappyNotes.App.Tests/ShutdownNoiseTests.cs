using YappyNotes.App;

namespace YappyNotes.App.Tests;

/// <summary>
/// Which exceptions escaping onto the dispatcher may be ignored. The answer is
/// almost none, and the tests are mostly about keeping it that way.
/// </summary>
public class ShutdownNoiseTests
{
    /// <summary>
    /// Avalonia 12.1.2's Linux tray cancels its D-Bus watch before it marks itself
    /// disposed, so the cancellation escapes an async void onto the dispatcher and
    /// aborts the process on the way out. Reported from real use and reproduced by
    /// disposing the tray while the app ran. The notes are already saved by then.
    /// </summary>
    [Fact]
    public void IsHarmless_ACancellationWhileShuttingDown_IsIgnored()
    {
        Assert.True(ShutdownNoise.IsHarmless(new TaskCanceledException(), shuttingDown: true));
    }

    [Fact]
    public void IsHarmless_ACancellationWhileRunning_IsNotIgnored()
    {
        Assert.False(ShutdownNoise.IsHarmless(new TaskCanceledException(), shuttingDown: false));
    }

    [Fact]
    public void IsHarmless_AnyOtherFailureWhileShuttingDown_IsNotIgnored()
    {
        Assert.False(ShutdownNoise.IsHarmless(new InvalidOperationException(), shuttingDown: true));
    }
}
