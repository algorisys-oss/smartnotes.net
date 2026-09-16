using SmartNotes.App.Views;
using SmartNotes.Core;
using SmartNotes.ViewModels;

namespace SmartNotes.App;

/// <summary>
/// Keeps one window - and one view-model - per note.
/// </summary>
/// <remarks>
/// Building view-models is this class's job rather than its callers', because
/// two view-models over one note would mean two <c>Note</c> objects, both held by
/// the autosave, racing each other's writes. Asking for a note already on screen
/// brings its window forward instead of opening a second one.
/// </remarks>
public sealed class WindowManager : IWindowManager
{
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;
    private readonly Dictionary<Guid, NoteWindow> _open = [];
    private readonly Dictionary<Guid, NoteViewModel> _viewModels = [];

    public WindowManager(NoteService notes, AutoSaveService autoSave)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(autoSave);

        _notes = notes;
        _autoSave = autoSave;
    }

    public IReadOnlyCollection<Guid> OpenNotes => _open.Keys;

    /// <summary>The live view-model for a note, if one has been built.</summary>
    public NoteViewModel? ViewModelFor(Guid noteId) => _viewModels.GetValueOrDefault(noteId);

    public async Task ShowNoteAsync(Guid noteId)
    {
        if (_open.TryGetValue(noteId, out var already))
        {
            already.Activate();
            return;
        }

        var viewModel = await ViewModelForAsync(noteId);
        if (viewModel is null)
        {
            // Archived or purged between the list being drawn and the click.
            return;
        }

        var window = new NoteWindow(viewModel);
        _open[noteId] = window;

        // However it closed - its own button, or the archive command - the
        // bookkeeping is the same. The view-model goes with the window: keeping
        // it would hand back a stale copy after an archive or a restore, and the
        // only reason to hold one at all is so that two windows cannot both be
        // editing the same note at once.
        window.Closed += (_, _) =>
        {
            _open.Remove(noteId);
            _viewModels.Remove(noteId);
        };

        window.Show();
    }

    public void CloseNote(Guid noteId)
    {
        if (_open.TryGetValue(noteId, out var window))
        {
            window.Close();
        }
    }

    private async Task<NoteViewModel?> ViewModelForAsync(Guid noteId)
    {
        if (_viewModels.TryGetValue(noteId, out var already))
        {
            return already;
        }

        var note = (await _notes.GetActiveAsync()).FirstOrDefault(n => n.Id == noteId);
        if (note is null)
        {
            return null;
        }

        var viewModel = new NoteViewModel(note, _notes, _autoSave, this);
        _viewModels[noteId] = viewModel;
        return viewModel;
    }
}
