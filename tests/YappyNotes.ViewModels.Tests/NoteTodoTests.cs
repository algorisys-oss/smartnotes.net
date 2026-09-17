using Microsoft.Extensions.Time.Testing;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

/// <summary>Adding to-dos to a note without writing their Markdown.</summary>
public class NoteTodoTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public NoteTodoTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, Debounce);
    }

    private async Task<NoteViewModel> NoteSayingAsync(string content)
    {
        var note = await _notes.CreateAsync(Token);
        note.Content = content;
        await _notes.SaveAsync(note, Token);
        return new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager());
    }

    [Fact]
    public async Task ShowsTodoAdder_OnANoteWithAChecklist_IsTrue()
    {
        var note = await NoteSayingAsync("- [ ] milk");

        Assert.True(note.ShowsTodoAdder);
    }

    [Fact]
    public async Task ShowsTodoAdder_OnANoteWithoutAChecklist_IsFalse()
    {
        var note = await NoteSayingAsync("back soon");

        Assert.False(note.ShowsTodoAdder);
    }

    /// <summary>In the editor the Markdown is right there, and Enter continues the list.</summary>
    [Fact]
    public async Task ShowsTodoAdder_WhileEditing_IsFalse()
    {
        var note = await NoteSayingAsync("- [ ] milk");

        note.BeginEditingAtEnd();

        Assert.False(note.ShowsTodoAdder);
    }

    [Fact]
    public async Task StartTodoList_OnANoteWithoutAChecklist_ShowsTheAdder()
    {
        var note = await NoteSayingAsync("back soon");

        note.StartTodoListCommand.Execute(null);

        Assert.True(note.ShowsTodoAdder);
    }

    [Fact]
    public async Task StartTodoList_WhileEditing_ShowsTheNoteFormattedWithTheAdder()
    {
        var note = await NoteSayingAsync("back soon");
        note.BeginEditingAtEnd();

        note.StartTodoListCommand.Execute(null);

        Assert.False(note.ShowsEditor);
        Assert.True(note.ShowsTodoAdder);
    }

    [Fact]
    public async Task StopAddingTodos_WithNothingTypedOnANoteWithoutAChecklist_HidesTheAdder()
    {
        var note = await NoteSayingAsync("back soon");
        note.StartTodoListCommand.Execute(null);

        note.StopAddingTodos();

        Assert.False(note.ShowsTodoAdder);
    }

    [Fact]
    public async Task AddTodo_WithText_AddsTheItemAndSavesIt()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.NewTodoText = "eggs";

        note.AddTodoCommand.Execute(null);
        _clock.Advance(Debounce);
        await _autoSave.WhenIdleAsync();

        Assert.Equal("- [ ] milk\n- [ ] eggs", (await _repository.GetByIdAsync(note.Id, Token))!.Content);
    }

    /// <summary>Emptied, ready for the next item - adding three things is three Enters.</summary>
    [Fact]
    public async Task AddTodo_WithText_EmptiesTheFieldForTheNextOne()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.NewTodoText = "eggs";

        note.AddTodoCommand.Execute(null);

        Assert.Equal(string.Empty, note.NewTodoText);
    }

    [Fact]
    public async Task AddTodo_WithNothingTyped_ChangesNothing()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.NewTodoText = "  ";

        note.AddTodoCommand.Execute(null);

        Assert.Equal("- [ ] milk", note.Content);
    }

    [Fact]
    public async Task ContinueListOnEnter_AtTheEndOfAnItem_AddsTheNextAndSaysWhereTheCaretGoes()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.BeginEditingAtEnd();

        var caret = note.ContinueListOnEnter("- [ ] milk".Length);

        Assert.Equal("- [ ] milk\n- [ ] ", note.Content);
        Assert.Equal("- [ ] milk\n- [ ] ".Length, caret);
    }

    [Fact]
    public async Task ContinueListOnEnter_OnPlainText_LeavesEnterToTheEditor()
    {
        var note = await NoteSayingAsync("back soon");
        note.BeginEditingAtEnd();

        var caret = note.ContinueListOnEnter("back soon".Length);

        Assert.Null(caret);
        Assert.Equal("back soon", note.Content);
    }

    [Fact]
    public async Task TodoProgressText_ForAChecklist_SaysHowManyAreDone()
    {
        var note = await NoteSayingAsync("- [x] bread\n- [ ] milk\n- [x] tea");

        Assert.Equal("2 of 3 done", note.TodoProgressText);
    }

    [Fact]
    public async Task TodoProgressText_AfterTickingOne_Updates()
    {
        var note = await NoteSayingAsync("- [ ] milk");

        await note.PressAsync(note.Lines[0], renderedIndex: 0);

        Assert.Equal("1 of 1 done", note.TodoProgressText);
    }

    [Fact]
    public async Task ShowsTodoFooter_ForANoteWithoutTodos_IsFalse()
    {
        var note = await NoteSayingAsync("back soon");

        Assert.False(note.ShowsTodoFooter);
    }

    [Fact]
    public async Task ClearCompleted_RemovesTheTickedItemsAndSaves()
    {
        var note = await NoteSayingAsync("- [x] bread\n- [ ] milk");

        note.ClearCompletedCommand.Execute(null);
        _clock.Advance(Debounce);
        await _autoSave.WhenIdleAsync();

        Assert.Equal("- [ ] milk", (await _repository.GetByIdAsync(note.Id, Token))!.Content);
    }

    /// <summary>
    /// Clearing deletes text, and losing a note to a mis-click is the one bug this
    /// app cannot afford - so the clear can be taken back, rather than asked about
    /// first every time.
    /// </summary>
    [Fact]
    public async Task UndoClear_StraightAfterClearing_PutsTheItemsBack()
    {
        const string content = "- [x] bread\n- [ ] milk";
        var note = await NoteSayingAsync(content);
        note.ClearCompletedCommand.Execute(null);

        Assert.True(note.CanUndoClear);
        note.UndoClearCommand.Execute(null);

        Assert.Equal(content, note.Content);
        Assert.False(note.CanUndoClear);
    }

    /// <summary>
    /// Once the note has changed again, putting the old text back would also undo
    /// that change, which nobody asked for.
    /// </summary>
    [Fact]
    public async Task CanUndoClear_AfterTheNoteChangedAgain_IsFalse()
    {
        var note = await NoteSayingAsync("- [x] bread\n- [ ] milk");
        note.ClearCompletedCommand.Execute(null);

        note.NewTodoText = "eggs";
        note.AddTodoCommand.Execute(null);

        Assert.False(note.CanUndoClear);
    }

    [Fact]
    public async Task ClearCompleted_OfEveryItem_KeepsTheFooterSoUndoCanBeReached()
    {
        var note = await NoteSayingAsync("back soon\n- [x] bread");

        note.ClearCompletedCommand.Execute(null);

        Assert.False(note.HasTodos);
        Assert.True(note.ShowsTodoFooter);
    }
}
