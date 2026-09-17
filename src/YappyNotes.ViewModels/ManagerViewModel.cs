using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YappyNotes.Core;

namespace YappyNotes.ViewModels;

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

    public ManagerViewModel(NoteService notes, IWindowManager windows, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(windows);

        _notes = notes;
        _windows = windows;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private readonly TimeProvider _timeProvider;

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

    partial void OnShowingArchiveChanged(bool value)
    {
        if (value)
        {
            ShowingTodos = false;
        }

        SearchCommand.Execute(null);
    }

    /// <summary>
    /// Whether the manager shows every open to-do, grouped by note, instead of the
    /// notes themselves. One view at a time: turning this on leaves the archive, and
    /// the archive's to-dos are not listed - they are filed away with their notes.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowingTodos { get; set; }

    /// <summary>The open to-dos, one group per note, newest note first.</summary>
    public ObservableCollection<TodoGroup> TodoGroups { get; } = [];

    partial void OnShowingTodosChanged(bool value)
    {
        if (value)
        {
            ShowingArchive = false;
        }

        SearchCommand.Execute(null);
    }

    /// <summary>
    /// Ticks a to-do in its note, from here. Through the window manager, never
    /// around it: a note open on the desktop has an autosave that would write its
    /// own copy over a tick made straight in storage.
    /// </summary>
    [RelayCommand]
    public async Task TickTodoAsync(TodoEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        await _windows.ChangeNoteAsync(entry.NoteId, content => TodoList.Tick(content, entry.Item));
        await RefreshAsync();
    }

    [RelayCommand]
    public Task OpenTodoNoteAsync(TodoGroup? group)
        => group is null ? Task.CompletedTask : _windows.ShowNoteAsync(group.NoteId);

    public bool IsEmpty => ShowingTodos ? TodoGroups.Count == 0 : Items.Count == 0;

    public string EmptyMessage => ShowingTodos
        ? string.IsNullOrWhiteSpace(SearchText) ? "Nothing left to do." : "No to-dos match that."
        : ShowingArchive
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

        await RefreshTodosAsync(cancellationToken);

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
    public void OpenSettings() => _windows.ShowSettings();

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

    private async Task RefreshTodosAsync(CancellationToken cancellationToken)
    {
        TodoGroups.Clear();

        if (!ShowingTodos)
        {
            return;
        }

        var search = SearchText.Trim();
        var today = DueStates.TodayBy(_timeProvider);
        var groups = new List<TodoGroup>();

        foreach (var note in (await _notes.GetActiveAsync(cancellationToken)).OrderByDescending(note => note.ModifiedUtc))
        {
            var title = NoteListItem.From(note).DisplayTitle;
            var titleMatches = search.Length > 0 && title.Contains(search, StringComparison.CurrentCultureIgnoreCase);

            var entries = TodoList.OpenItems(note.Content)
                .Where(item => search.Length == 0 || titleMatches || item.Text.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .Select(item => new TodoEntry(note.Id, item) { Today = today })
                .ToList();

            if (entries.Count > 0)
            {
                groups.Add(new TodoGroup(note.Id, title, note.Color, entries));
            }
        }

        // Soonest due first: this view is for "what do I do next". Notes with no
        // dates keep the newest-first order after them - OrderBy is stable.
        foreach (var group in groups.OrderBy(group => group.Items.Min(item => item.Item.Due) ?? DateOnly.MaxValue))
        {
            TodoGroups.Add(group);
        }
    }

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
