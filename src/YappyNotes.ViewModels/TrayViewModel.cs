using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>
/// The tray icon's menu: what is left to click when every window is shut.
/// </summary>
/// <remarks>
/// The app runs all day, and closing the manager no longer ends it, so the tray
/// is the one place guaranteed to be there. Each item goes through
/// <see cref="IWindowManager"/> rather than opening anything itself, which keeps
/// "one note, one window" true however many ways there are to ask for a note.
/// </remarks>
public sealed partial class TrayViewModel : ObservableObject
{
    private readonly NoteService _notes;
    private readonly IWindowManager _windows;
    private readonly IAppLifetime _lifetime;

    public TrayViewModel(NoteService notes, IWindowManager windows, IAppLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(lifetime);

        _notes = notes;
        _windows = windows;
        _lifetime = lifetime;
    }

    [RelayCommand]
    public async Task NewNoteAsync()
    {
        var note = await _notes.CreateAsync();
        await _windows.ShowNoteAsync(note.Id);
    }

    [RelayCommand]
    public async Task ShowAllNotesAsync()
    {
        foreach (var note in await _notes.GetActiveAsync())
        {
            await _windows.ShowNoteAsync(note.Id);
        }
    }

    /// <summary>
    /// Closes every note window. The notes stay on the desktop - closing flushes
    /// and archives nothing - so Show all notes, or the next start, brings them
    /// back where they were.
    /// </summary>
    [RelayCommand]
    public void HideAllNotes()
    {
        // A copy, because each close takes its note out of the collection.
        foreach (var noteId in _windows.OpenNotes.ToList())
        {
            _windows.CloseNote(noteId);
        }
    }

    [RelayCommand]
    public void OpenManager() => _windows.ShowManager();

    [RelayCommand]
    public void Quit() => _lifetime.Quit();
}
