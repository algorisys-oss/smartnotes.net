using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>
/// Runs the work where it was posted from, so a test sees the result without a
/// UI thread existing.
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public int Posted { get; private set; }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Posted++;
        action();
    }
}
