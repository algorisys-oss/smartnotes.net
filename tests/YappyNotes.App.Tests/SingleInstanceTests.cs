using YappyNotes.App;

namespace YappyNotes.App.Tests;

/// <summary>
/// One running copy per notes folder, with no window anywhere.
/// </summary>
/// <remarks>
/// Two copies in one test process stand in for two processes. That holds because
/// the lock is taken per open file, not per process - on Unix .NET takes
/// <c>flock</c> on each handle, and Windows refuses a second handle outright - and
/// it was checked across two real processes as well.
/// </remarks>
public sealed class SingleInstanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"yappynotes-instance-{Guid.CreateVersion7()}");

    private readonly List<IDisposable> _held = [];

    public SingleInstanceTests() => Directory.CreateDirectory(_directory);

    private SingleInstance? Claim(string? directory = null)
    {
        var claimed = SingleInstance.TryClaim(directory ?? _directory);
        if (claimed is not null)
        {
            _held.Add(claimed);
        }

        return claimed;
    }

    [Fact]
    public void TryClaim_OnAFolderNobodyHolds_Succeeds()
    {
        var claimed = Claim();

        Assert.NotNull(claimed);
    }

    [Fact]
    public void TryClaim_WhileAnotherCopyHoldsTheFolder_IsRefused()
    {
        Claim();

        var second = Claim();

        Assert.Null(second);
    }

    [Fact]
    public void TryClaim_AfterTheHolderLetGo_Succeeds()
    {
        Claim()!.Dispose();

        var second = Claim();

        Assert.NotNull(second);
    }

    /// <summary>
    /// The lock is per notes folder, not per machine, so a
    /// <c>dev-start.sh --sandbox</c> copy can run beside the reader's own.
    /// </summary>
    [Fact]
    public void TryClaim_ForADifferentNotesFolder_IsNotRefused()
    {
        var sandbox = Path.Combine(_directory, "sandbox");
        Directory.CreateDirectory(sandbox);
        Claim();

        var second = Claim(sandbox);

        Assert.NotNull(second);
    }

    [Fact]
    public async Task TryWakeRunningCopy_WhileTheHolderListens_ReachesIt()
    {
        var woken = new TaskCompletionSource();
        Claim()!.Listen(() => woken.TrySetResult());

        var reached = SingleInstance.TryWakeRunningCopy(_directory, Patience);

        Assert.True(reached, "the second start could not reach the copy holding the folder");
        await woken.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TryWakeRunningCopy_StartedTwiceInARow_WakesTheHolderTwice()
    {
        var wakes = 0;
        var secondWake = new TaskCompletionSource();
        Claim()!.Listen(() =>
        {
            if (Interlocked.Increment(ref wakes) == 2)
            {
                secondWake.TrySetResult();
            }
        });

        SingleInstance.TryWakeRunningCopy(_directory, Patience);
        SingleInstance.TryWakeRunningCopy(_directory, Patience);

        await secondWake.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.Equal(2, wakes);
    }

    [Fact]
    public void TryWakeRunningCopy_WithNobodyListening_GivesUpAndSaysSo()
    {
        var reached = SingleInstance.TryWakeRunningCopy(_directory, TimeSpan.FromMilliseconds(200));

        Assert.False(reached, "the second start reached something although no copy is listening");
    }

    /// <summary>
    /// Letting go has to stop the listening too, or a copy that has quit keeps
    /// answering and the next start believes something is running.
    /// </summary>
    [Fact]
    public void TryWakeRunningCopy_AfterTheHolderLetGo_FindsNobody()
    {
        var claimed = Claim()!;
        claimed.Listen(() => { });
        claimed.Dispose();

        var reached = SingleInstance.TryWakeRunningCopy(_directory, TimeSpan.FromMilliseconds(200));

        Assert.False(reached, "the second start reached something although no copy is listening");
    }

    public void Dispose()
    {
        foreach (var held in _held)
        {
            held.Dispose();
        }

        Directory.Delete(_directory, recursive: true);
    }
}
