using Microsoft.Extensions.Time.Testing;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

/// <summary>Every to-do still open, from every note, in one place.</summary>
public class ManagerTodoTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly FakeWindowManager _windows;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ManagerTodoTests()
    {
        _notes = new NoteService(_repository, _clock);
        _windows = new FakeWindowManager { Notes = _notes };
    }

    private async Task<Note> NoteAsync(string title, string content, bool archived = false)
    {
        var note = await _notes.CreateAsync(Token);
        note.Title = title;
        note.Content = content;
        await _notes.SaveAsync(note, Token);
        if (archived)
        {
            await _notes.ArchiveAsync(note.Id, Token);
        }

        _clock.Advance(TimeSpan.FromMinutes(1));
        return note;
    }

    private async Task<ManagerViewModel> ShowingTodosAsync()
    {
        var manager = new ManagerViewModel(_notes, _windows) { ShowingTodos = true };
        await manager.RefreshAsync(Token);
        return manager;
    }

    /// <summary>Each group as "Note: item, item", so a whole list compares as text.</summary>
    private static IReadOnlyList<string> Shown(ManagerViewModel manager)
        => [.. manager.TodoGroups.Select(group => $"{group.Title}: {string.Join(", ", group.Items.Select(item => item.Text))}")];

    [Fact]
    public async Task ShowingTodos_ListsEveryOpenTodoUnderItsNote()
    {
        await NoteAsync("Shopping", "- [ ] milk\n- [x] bread\n- [ ] eggs");
        await NoteAsync("Stream", "- [ ] scene switcher");
        await NoteAsync("Plain", "back soon");

        var manager = await ShowingTodosAsync();

        // Newest note first, as the note list is.
        Assert.Equal(
            ["Stream: scene switcher", "Shopping: milk, eggs"],
            Shown(manager));
    }

    [Fact]
    public async Task ShowingTodos_LeavesAnArchivedNotesTodosOut()
    {
        await NoteAsync("Old", "- [ ] forgotten", archived: true);

        var manager = await ShowingTodosAsync();

        Assert.Empty(manager.TodoGroups);
        Assert.True(manager.IsEmpty);
        Assert.Equal("Nothing left to do.", manager.EmptyMessage);
    }

    [Fact]
    public void ShowingTodos_WhenTurnedOn_LeavesTheArchive()
    {
        var manager = new ManagerViewModel(_notes, _windows) { ShowingArchive = true };

        manager.ShowingTodos = true;

        Assert.False(manager.ShowingArchive);
    }

    [Fact]
    public async Task ShowingArchive_WhenTurnedOn_LeavesTheTodos()
    {
        var manager = await ShowingTodosAsync();

        manager.ShowingArchive = true;

        Assert.False(manager.ShowingTodos);
    }

    [Fact]
    public async Task SearchText_WhileShowingTodos_KeepsTheMatchingItems()
    {
        await NoteAsync("Shopping", "- [ ] oat milk\n- [ ] eggs");
        var manager = await ShowingTodosAsync();

        manager.SearchText = "milk";
        await manager.SearchCommand.ExecutionTask!;

        Assert.Equal(["Shopping: oat milk"], Shown(manager));
    }

    [Fact]
    public async Task SearchText_MatchingANotesTitle_KeepsAllOfItsTodos()
    {
        await NoteAsync("Shopping", "- [ ] oat milk\n- [ ] eggs");
        await NoteAsync("Stream", "- [ ] scene switcher");
        var manager = await ShowingTodosAsync();

        manager.SearchText = "shop";
        await manager.SearchCommand.ExecutionTask!;

        Assert.Equal(["Shopping: oat milk, eggs"], Shown(manager));
    }

    [Fact]
    public async Task TickTodoAsync_TicksItInItsNoteAndTakesItOffTheList()
    {
        var shopping = await NoteAsync("Shopping", "- [ ] milk\n- [ ] eggs");
        var manager = await ShowingTodosAsync();

        await manager.TickTodoCommand.ExecuteAsync(manager.TodoGroups[0].Items[0]);

        Assert.Equal("- [x] milk\n- [ ] eggs", (await _repository.GetByIdAsync(shopping.Id, Token))!.Content);
        Assert.Equal(["Shopping: eggs"], Shown(manager));
    }

    /// <summary>
    /// Never around the note's window: an open note's autosave would write its
    /// own copy straight over a tick made in storage.
    /// </summary>
    [Fact]
    public async Task TickTodoAsync_GoesThroughTheWindowManager()
    {
        var shopping = await NoteAsync("Shopping", "- [ ] milk");
        var manager = await ShowingTodosAsync();

        await manager.TickTodoCommand.ExecuteAsync(manager.TodoGroups[0].Items[0]);

        Assert.Equal([shopping.Id], _windows.Changed);
    }

    [Fact]
    public async Task OpenTodoNote_OpensTheNoteTheGroupBelongsTo()
    {
        var shopping = await NoteAsync("Shopping", "- [ ] milk");
        var manager = await ShowingTodosAsync();

        await manager.OpenTodoNoteCommand.ExecuteAsync(manager.TodoGroups[0]);

        Assert.Equal([shopping.Id], _windows.Shown);
    }
}
