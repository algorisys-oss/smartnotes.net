using Avalonia.Controls;
using SmartNotes.App.Views;
using SmartNotes.ViewModels;

namespace SmartNotes.App;

/// <summary>
/// Keeps one window per note, and knows which is which.
/// </summary>
/// <remarks>
/// Asking for a note that is already on screen brings its window forward rather
/// than opening a second one - two windows over the same note would both be
/// editing the same object and racing each other's autosave.
/// </remarks>
public sealed class WindowManager : IWindowManager
{
    private readonly Dictionary<Guid, NoteWindow> _open = [];

    public IReadOnlyCollection<Guid> OpenNotes => _open.Keys;

    public void ShowNote(NoteViewModel note)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (_open.TryGetValue(note.Id, out var already))
        {
            already.Activate();
            return;
        }

        var window = new NoteWindow(note);
        _open[note.Id] = window;

        // Whether it closed by its own button or by the archive command, the
        // bookkeeping is the same.
        window.Closed += (_, _) => _open.Remove(note.Id);

        window.Show();
    }

    public void CloseNote(Guid noteId)
    {
        if (_open.TryGetValue(noteId, out var window))
        {
            window.Close();
        }
    }
}
