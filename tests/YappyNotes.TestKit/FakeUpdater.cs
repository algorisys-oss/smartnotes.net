using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>An updater that never touches the network, told what to find.</summary>
public sealed class FakeUpdater : IUpdater
{
    public bool CanUpdate { get; set; } = true;

    /// <summary>What a check finds. Null means this is the latest.</summary>
    public string? Newer { get; set; }

    /// <summary>When set, a check fails with this.</summary>
    public Exception? CheckFailure { get; set; }

    /// <summary>When set, a check does not finish until this does.</summary>
    public TaskCompletionSource? HoldCheck { get; set; }

    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public bool AppliedOnExit { get; private set; }

    public async Task<string?> CheckAsync(CancellationToken cancellationToken = default)
    {
        Checks++;
        if (HoldCheck is not null)
        {
            await HoldCheck.Task;
        }

        return CheckFailure is null ? Newer : throw CheckFailure;
    }

    public Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        Downloads++;
        return Task.CompletedTask;
    }

    public void ApplyOnExit() => AppliedOnExit = true;
}
