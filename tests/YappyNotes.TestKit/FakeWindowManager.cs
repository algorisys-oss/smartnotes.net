using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>
/// Records what a view-model asked the windowing layer to do, so that view-model
/// tests can assert on the request without a window existing.
/// </summary>
public sealed class FakeWindowManager : IWindowManager
{
    public List<Guid> Shown { get; } = [];
    public List<Guid> Closed { get; } = [];

    public Task ShowNoteAsync(Guid noteId)
    {
        Shown.Add(noteId);
        return Task.CompletedTask;
    }

    public void CloseNote(Guid noteId) => Closed.Add(noteId);

    public int SettingsShown { get; private set; }

    public void ShowSettings() => SettingsShown++;
}
