using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

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
    private readonly Func<SettingsViewModel>? _newSettings;
    private SettingsWindow? _settingsWindow;
    private readonly Dictionary<Guid, NoteWindow> _open = [];
    private readonly Dictionary<Guid, NoteViewModel> _viewModels = [];

    /// <param name="newSettings">
    /// How to build the settings view-model, when one is wanted. A factory
    /// rather than an instance so that nothing reads the database until someone
    /// actually opens the window - and optional, because the tests that are
    /// about note windows have no business standing up a settings store.
    /// </param>
    public WindowManager(NoteService notes, AutoSaveService autoSave, Func<SettingsViewModel>? newSettings = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(autoSave);

        _notes = notes;
        _autoSave = autoSave;
        _newSettings = newSettings;
    }

    public IReadOnlyCollection<Guid> OpenNotes => _open.Keys;

    /// <summary>The live view-model for a note, if one has been built.</summary>
    public NoteViewModel? ViewModelFor(Guid noteId) => _viewModels.GetValueOrDefault(noteId);

    /// <summary>The settings window's view-model, while that window is open.</summary>
    public SettingsViewModel? OpenSettings => _settingsWindow?.DataContext as SettingsViewModel;

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

    public void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        if (_newSettings is null)
        {
            return;
        }

        var settings = _newSettings();
        _settingsWindow = new SettingsWindow(settings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();

        // After Show, so the controls exist to be filled in.
        _ = settings.LoadAsync();
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

        var viewModel = new NoteViewModel(
            note, _notes, _autoSave, this,
            TimeProvider.System,
            new AvaloniaUiDispatcher(),
            new AvaloniaLinkLauncher(() => _open.GetValueOrDefault(note.Id)));
        _viewModels[noteId] = viewModel;
        return viewModel;
    }
}
