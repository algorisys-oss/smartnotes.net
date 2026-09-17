using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>
/// Records what a view-model asked the windowing layer to do, so that view-model
/// tests can assert on the request without a window existing.
/// </summary>
public sealed class FakeWindowManager : IWindowManager
{
    private readonly List<Guid> _open = [];

    public List<Guid> Shown { get; } = [];
    public List<Guid> Closed { get; } = [];

    public IReadOnlyCollection<Guid> OpenNotes => _open;

    public Task ShowNoteAsync(Guid noteId)
    {
        Shown.Add(noteId);
        if (!_open.Contains(noteId))
        {
            _open.Add(noteId);
        }

        return Task.CompletedTask;
    }

    public void CloseNote(Guid noteId)
    {
        Closed.Add(noteId);
        _open.Remove(noteId);
    }

    public int SettingsShown { get; private set; }

    public void ShowSettings() => SettingsShown++;

    /// <summary>
    /// Where <see cref="ChangeNoteAsync"/> writes, when a test wants the change to
    /// land. Left null, changes are only recorded.
    /// </summary>
    public YappyNotes.Core.NoteService? Notes { get; init; }

    public List<Guid> Changed { get; } = [];

    public async Task ChangeNoteAsync(Guid noteId, Func<string, string> changeContent)
    {
        Changed.Add(noteId);

        if (Notes is null || (await Notes.GetActiveAsync()).FirstOrDefault(note => note.Id == noteId) is not { } note)
        {
            return;
        }

        note.Content = changeContent(note.Content);
        await Notes.SaveAsync(note);
    }

    public int ManagerShown { get; private set; }

    public void ShowManager() => ManagerShown++;
}
