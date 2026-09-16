using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartNotes.Core;

namespace SmartNotes.ViewModels;

/// <summary>
/// The manager window: everything you have, and the ways back to it.
/// </summary>
/// <remarks>
/// It shows one of two lists - the desktop or the archive - and searches
/// whichever it is showing. The list is rebuilt from the service on demand rather
/// than kept in step with the open windows: a note window and this share nothing
/// but the database, and refreshing when the manager is looked at is both simpler
/// and harder to get wrong than a change-notification web between them.
/// </remarks>
public sealed partial class ManagerViewModel : ObservableObject
{
    private readonly NoteService _notes;
    private readonly IWindowManager _windows;

    public ManagerViewModel(NoteService notes, IWindowManager windows)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(windows);

        _notes = notes;
        _windows = windows;
    }

    public ObservableCollection<NoteListItem> Items { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowingArchive { get; set; }

    // The list follows these two, and the trigger lives here rather than on the
    // controls that set them. Wiring a TextBox's TextChanged to the search
    // instead looks equivalent and is not: the event and the binding that writes
    // SearchText have no guaranteed order between them, so the search would run
    // against the previous value - a list one keystroke behind what was typed.
    partial void OnSearchTextChanged(string value) => SearchCommand.Execute(null);

    partial void OnShowingArchiveChanged(bool value) => SearchCommand.Execute(null);

    public bool IsEmpty => Items.Count == 0;

    public string EmptyMessage => ShowingArchive
        ? "Nothing archived yet."
        : string.IsNullOrWhiteSpace(SearchText)
            ? "No notes yet. Make one."
            : "No notes match that.";

    /// <summary>Rebuilds the list from what is stored.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var notes = await CurrentListAsync(cancellationToken);

        Items.Clear();
        // Newest first: the manager is for finding the note you were just
        // working on, which is the other way round from the repository's
        // oldest-first ordering.
        foreach (var note in notes.OrderByDescending(note => note.ModifiedUtc))
        {
            Items.Add(NoteListItem.From(note));
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    [RelayCommand]
    public Task SearchAsync() => RefreshAsync();

    [RelayCommand]
    public async Task NewNoteAsync()
    {
        var note = await _notes.CreateAsync();
        await _windows.ShowNoteAsync(note.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    public Task OpenAsync(NoteListItem? item)
        => item is null ? Task.CompletedTask : _windows.ShowNoteAsync(item.Id);

    [RelayCommand]
    public async Task ArchiveAsync(NoteListItem? item)
    {
        if (item is null)
        {
            return;
        }

        // Before archiving, so that whatever its window was still holding is
        // written rather than discarded.
        _windows.CloseNote(item.Id);
        await _notes.ArchiveAsync(item.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RestoreAsync(NoteListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _notes.RestoreAsync(item.Id);
        await RefreshAsync();
    }

    /// <summary>
    /// Destroys a note. Offered only in the archive, which is the whole safety
    /// property: nothing a reader can press in one action destroys a note still
    /// on their desktop.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteForever))]
    public async Task DeleteForeverAsync(NoteListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _notes.PurgeAsync(item.Id);
        await RefreshAsync();
    }

    private static bool CanDeleteForever(NoteListItem? item) => item?.IsArchived ?? false;

    private Task<IReadOnlyList<Note>> CurrentListAsync(CancellationToken cancellationToken)
    {
        var searching = !string.IsNullOrWhiteSpace(SearchText);

        return (ShowingArchive, searching) switch
        {
            (true, true) => _notes.SearchArchivedAsync(SearchText, cancellationToken),
            (true, false) => _notes.GetArchivedAsync(cancellationToken),
            (false, true) => _notes.SearchAsync(SearchText, cancellationToken),
            (false, false) => _notes.GetActiveAsync(cancellationToken),
        };
    }
}
