using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>A login item that records what it was told instead of telling the OS.</summary>
public sealed class FakeLoginItem : ILoginItem
{
    public bool IsAvailable { get; set; } = true;

    /// <summary>When set, the next <see cref="Set"/> fails with this.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Every value <see cref="Set"/> was called with, in order.</summary>
    public List<bool> Sets { get; } = [];

    public void Set(bool startAtLogin)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Sets.Add(startAtLogin);
    }
}
