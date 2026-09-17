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
        return new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager(), _clock);
    }

    [Fact]
    public async Task ShowsTodoPrompt_OnANoteWithAChecklist_IsTrue()
    {
        var note = await NoteSayingAsync("- [ ] milk");

        Assert.True(note.ShowsTodoPrompt);
    }

    [Fact]
    public async Task ShowsTodoPrompt_OnANoteWithoutAChecklist_IsFalse()
    {
        var note = await NoteSayingAsync("back soon");

        Assert.False(note.ShowsTodoPrompt);
    }

    /// <summary>In the editor the Markdown is right there, and Enter continues a list.</summary>
    [Fact]
    public async Task ShowsTodoPrompt_WhileEditing_IsFalse()
    {
        var note = await NoteSayingAsync("- [ ] milk");

        note.BeginEditingAtEnd();

        Assert.False(note.ShowsTodoPrompt);
    }

    /// <summary>
    /// Adding happens on a new line of the list itself, with its own box, rather
    /// than in a separate field under the note - reported from real use, where a
    /// field below with the item appearing above it read as the note misbehaving.
    /// </summary>
    [Fact]
    public async Task StartTodoList_OnANoteWithoutAChecklist_OpensANewLineToTypeOn()
    {
        var note = await NoteSayingAsync("back soon");

        note.StartTodoListCommand.Execute(null);

        Assert.True(note.IsAddingTodo);
        Assert.False(note.ShowsTodoPrompt);
    }

    [Fact]
    public async Task StartTodoList_WhileEditing_ShowsTheNoteFormattedWithTheNewLine()
    {
        var note = await NoteSayingAsync("back soon");
        note.BeginEditingAtEnd();

        note.StartTodoListCommand.Execute(null);

        Assert.False(note.ShowsEditor);
        Assert.True(note.IsAddingTodo);
    }

    [Fact]
    public async Task AddTodo_WithText_AddsTheItemAndSavesIt()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.StartTodoListCommand.Execute(null);
        note.NewTodoText = "eggs";

        note.AddTodoCommand.Execute(null);
        _clock.Advance(Debounce);
        await _autoSave.WhenIdleAsync();

        Assert.Equal("- [ ] milk\n- [ ] eggs", (await _repository.GetByIdAsync(note.Id, Token))!.Content);
    }

    /// <summary>Enter starts the next item, as it does in the editor's own lists.</summary>
    [Fact]
    public async Task AddTodo_WithText_LeavesANewEmptyLineForTheNextItem()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.StartTodoListCommand.Execute(null);
        note.NewTodoText = "eggs";

        note.AddTodoCommand.Execute(null);

        Assert.True(note.IsAddingTodo);
        Assert.Equal(string.Empty, note.NewTodoText);
    }

    /// <summary>Enter on an empty item finishes the list - the same rule as the editor's.</summary>
    [Fact]
    public async Task AddTodo_OnAnEmptyLine_FinishesAdding()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.StartTodoListCommand.Execute(null);

        note.AddTodoCommand.Execute(null);

        Assert.False(note.IsAddingTodo);
        Assert.Equal("- [ ] milk", note.Content);
    }

    [Fact]
    public async Task CancelAddingTodo_ThrowsAwayWhatWasTyped()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.StartTodoListCommand.Execute(null);
        note.NewTodoText = "eg";

        note.CancelAddingTodo();

        Assert.False(note.IsAddingTodo);
        Assert.Equal(string.Empty, note.NewTodoText);
        Assert.Equal("- [ ] milk", note.Content);
    }

    /// <summary>
    /// Clicking away is not "never mind": what was typed becomes the item rather than
    /// being lost. Escape is how to throw it away.
    /// </summary>
    [Fact]
    public async Task StopAddingTodos_WithSomethingTyped_KeepsItAsATodo()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.StartTodoListCommand.Execute(null);
        note.NewTodoText = "eggs";

        note.StopAddingTodos();

        Assert.False(note.IsAddingTodo);
        Assert.Equal("- [ ] milk\n- [ ] eggs", note.Content);
    }

    [Fact]
    public async Task StopAddingTodos_WithNothingTyped_JustFinishes()
    {
        var note = await NoteSayingAsync("back soon");
        note.StartTodoListCommand.Execute(null);

        note.StopAddingTodos();

        Assert.False(note.IsAddingTodo);
        Assert.Equal("back soon", note.Content);
    }

    /// <summary>
    /// The new line is drawn where the item will be stored - straight after the last
    /// to-do, at its indent - not at the bottom of the note.
    /// </summary>
    [Fact]
    public async Task NewTodoPosition_OnANoteWithTextAfterItsList_IsStraightAfterTheLastTodo()
    {
        var note = await NoteSayingAsync("# Shop\n- [ ] milk\n  - [ ] oat\nthanks");

        Assert.Equal(3, note.NewTodoPosition);
        Assert.Equal(1, note.NewTodoLevel);
    }

    [Fact]
    public async Task NewTodoPosition_OnANoteWithoutAChecklist_IsTheEnd()
    {
        var note = await NoteSayingAsync("back soon\nsee you");

        Assert.Equal(2, note.NewTodoPosition);
        Assert.Equal(0, note.NewTodoLevel);
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

        note.StartTodoListCommand.Execute(null);
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

    [Fact]
    public async Task AddTodo_WithTomorrowTyped_StoresTheDateItMeans()
    {
        var note = await NoteSayingAsync("- [ ] milk");
        note.StartTodoListCommand.Execute(null);
        note.NewTodoText = "call Sam @tomorrow";

        note.AddTodoCommand.Execute(null);

        Assert.Equal("- [ ] milk\n- [ ] call Sam @2026-09-18", note.Content);
    }

    [Theory]
    [InlineData("- [ ] rent @2026-09-16", DueState.Overdue)]
    [InlineData("- [ ] rent @2026-09-17", DueState.Today)]
    [InlineData("- [ ] rent @2026-09-18", DueState.Later)]
    public async Task Lines_ForATodoWithADueDate_KnowHowSoonItIs(string content, DueState expected)
    {
        var note = await NoteSayingAsync(content);

        var due = note.Lines[0].Block.Runs.Single(run => run.Due is not null);

        Assert.Equal(expected, note.Lines[0].DueStateOf(due));
    }

    /// <summary>A ticked item is not late, however long ago its date was.</summary>
    [Fact]
    public async Task Lines_ForATickedTodoPastItsDate_AreNotOverdue()
    {
        var note = await NoteSayingAsync("- [x] rent @2026-09-01");

        var due = note.Lines[0].Block.Runs.Single(run => run.Due is not null);

        Assert.Equal(DueState.Later, note.Lines[0].DueStateOf(due));
    }

    /// <summary>
    /// Reported from real use, with a screenshot: on an empty note the first letter
    /// typed became the note's text and the new box appeared under it. An empty note
    /// shows the editor so a new note is ready to type into - and that rule was
    /// still hiding the formatted note, and the new line in it, while adding.
    /// </summary>
    [Fact]
    public async Task StartTodoList_OnAnEmptyNote_ShowsTheNewLineRatherThanTheEditor()
    {
        var note = await NoteSayingAsync(string.Empty);

        note.StartTodoListCommand.Execute(null);

        Assert.False(note.ShowsEditor);
        Assert.True(note.IsAddingTodo);
    }

    /// <summary>Once nothing is being added, an empty note is ready to type into again.</summary>
    [Fact]
    public async Task CancelAddingTodo_OnAnEmptyNote_ShowsTheEditorAgain()
    {
        var note = await NoteSayingAsync(string.Empty);
        note.StartTodoListCommand.Execute(null);

        note.CancelAddingTodo();

        Assert.True(note.ShowsEditor);
    }

    /// <summary>
    /// A note of nothing but ticked to-dos is empty once cleared, and an empty note
    /// shows the editor - which would hide the footer, and the Undo in it, at the one
    /// moment Undo matters.
    /// </summary>
    [Fact]
    public async Task ClearCompleted_OnANoteOfOnlyTickedTodos_StillOffersUndo()
    {
        var note = await NoteSayingAsync("- [x] bread\n- [x] milk");

        note.ClearCompletedCommand.Execute(null);

        Assert.Equal(string.Empty, note.Content);
        Assert.False(note.ShowsEditor);
        Assert.True(note.ShowsTodoFooter);

        note.UndoClearCommand.Execute(null);

        Assert.Equal("- [x] bread\n- [x] milk", note.Content);
        Assert.False(note.IsEditing, "undoing a clear opened the Markdown editor");
    }
}
