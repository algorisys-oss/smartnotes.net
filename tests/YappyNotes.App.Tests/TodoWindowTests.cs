using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

/// <summary>Adding to-dos from the note window, with the keyboard as it is really pressed.</summary>
public class TodoWindowTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public TodoWindowTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
    }

    private NoteWindow OpenWith(string content)
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        note.Content = content;

        var window = new NoteWindow(new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager()));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static NoteViewModel NoteOf(NoteWindow window) => (NoteViewModel)window.DataContext!;

    private static TextBox Adder(NoteWindow window)
        => window.FindControl<TextBox>("TodoAdder") ?? throw new InvalidOperationException("the note has no TodoAdder field");

    private static bool ShowsAdder(NoteWindow window)
        => window.FindControl<ScrollViewer>("FormattedScroll")!.IsVisible && Adder(window).IsVisible;

    private static void Press(NoteWindow window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Prompt(NoteWindow window)
        => window.FindControl<Button>("TodoAddPrompt") ?? throw new InvalidOperationException("the note has no TodoAddPrompt");

    /// <summary>Clicks a control where a person would, rather than calling its command.</summary>
    private static void Click(NoteWindow window, Control control)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void FormattedChecklist_EndsWithAPromptToAddATodo()
    {
        var window = OpenWith("- [ ] milk");

        Assert.True(Prompt(window).IsVisible);
        Assert.False(Adder(window).IsVisible, "a text box shows before anyone asked to add");
    }

    /// <summary>
    /// Reported from real use: typing into a field under the note, with each item
    /// appearing above it, read as the note misbehaving. The new item is typed on
    /// its own line of the list, where it will be.
    /// </summary>
    [AvaloniaFact]
    public void ClickingThePrompt_OpensANewLineInTheListWithTheKeyboardOnIt()
    {
        var window = OpenWith("- [ ] milk\nthanks");

        Click(window, Prompt(window));

        var row = Named<Control>(window, "NewTodoRow");
        Assert.True(Adder(window).IsFocused, "the new line did not take the keyboard");
        Assert.False(NoteOf(window).IsEditing, "adding opened the Markdown editor");
        Assert.Equal(1, Named<FormattedNote>(window, "Formatted").Children.IndexOf(row));
    }

    [AvaloniaFact]
    public void NewLine_TypingAndPressingEnter_AddsTheTodoAndOpensTheNextLine()
    {
        var window = OpenWith("- [ ] milk");
        Click(window, Prompt(window));

        window.KeyTextInput("eggs");
        Press(window, Key.Enter);

        Assert.Equal("- [ ] milk\n- [ ] eggs", NoteOf(window).Content);
        Assert.Equal(string.Empty, Adder(window).Text);
        Assert.True(Adder(window).IsFocused, "the next line did not keep the keyboard");
        Assert.True(NoteOf(window).IsAddingTodo);
    }

    [AvaloniaFact]
    public void NewLine_EnterWithNothingTyped_FinishesAdding()
    {
        var window = OpenWith("- [ ] milk");
        Click(window, Prompt(window));

        Press(window, Key.Enter);

        Assert.False(NoteOf(window).IsAddingTodo);
        Assert.True(Prompt(window).IsVisible);
        Assert.Equal("- [ ] milk", NoteOf(window).Content);
    }

    /// <summary>Escape on the new line is "never mind", not "close this note".</summary>
    [AvaloniaFact]
    public void NewLine_Escape_ThrowsItAwayWithoutClosingTheNote()
    {
        var window = OpenWith("- [ ] milk");
        Click(window, Prompt(window));
        window.KeyTextInput("eg");

        Press(window, Key.Escape);

        Assert.True(window.IsVisible);
        Assert.False(NoteOf(window).IsAddingTodo);
        Assert.Equal("- [ ] milk", NoteOf(window).Content);
    }

    [AvaloniaFact]
    public void AddATodoFromTheMenu_OnANoteWithoutAChecklist_OpensANewLineWithTheKeyboardOnIt()
    {
        var window = OpenWith("back soon");
        var chrome = window.FindControl<Border>("NoteChrome")!;

        // Opened first, as a right-click would: a menu's items are not bound to the
        // note until then, and their commands are still null.
        chrome.ContextMenu!.Open(chrome);
        Dispatcher.UIThread.RunJobs();
        var item = chrome.ContextMenu.Items
            .OfType<MenuItem>()
            .Single(menuItem => (string?)menuItem.Header == "Add a to-do");
        chrome.ContextMenu.Close();

        item.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Adder(window).IsVisible);
        Assert.True(Adder(window).IsFocused);
    }

    [AvaloniaFact]
    public void EditorEnter_AtTheEndOfATodo_StartsTheNextOne()
    {
        var window = OpenWith("- [ ] milk");
        NoteOf(window).BeginEditingAtEnd();
        Dispatcher.UIThread.RunJobs();
        var editor = window.FindControl<TextBox>("Body")!;

        Press(window, Key.Enter);

        Assert.Equal("- [ ] milk\n- [ ] ", editor.Text);
        Assert.Equal(editor.Text!.Length, editor.CaretIndex);
    }

    [AvaloniaFact]
    public void EditorShiftEnter_AtTheEndOfATodo_IsAnOrdinaryLineBreak()
    {
        var window = OpenWith("- [ ] milk");
        NoteOf(window).BeginEditingAtEnd();
        Dispatcher.UIThread.RunJobs();
        var editor = window.FindControl<TextBox>("Body")!;

        Press(window, Key.Enter, RawInputModifiers.Shift);

        Assert.DoesNotContain("- [ ] milk\n- [ ]", editor.Text, StringComparison.Ordinal);
        Assert.StartsWith("- [ ] milk", editor.Text, StringComparison.Ordinal);
    }

    private static T Named<T>(NoteWindow window, string name)
        where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"the note has no {name}");

    [AvaloniaFact]
    public void FormattedChecklist_SaysHowManyAreDone()
    {
        var window = OpenWith("- [x] bread\n- [ ] milk");

        Assert.True(Named<Control>(window, "TodoFooter").IsVisible);
        Assert.Equal("1 of 2 done", Named<TextBlock>(window, "TodoProgress").Text);
    }

    [AvaloniaFact]
    public void ClearCompleted_RemovesTheTickedItemsAndOffersUndo()
    {
        var window = OpenWith("- [x] bread\n- [ ] milk");

        Named<Button>(window, "TodoClear").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("- [ ] milk", NoteOf(window).Content);
        Assert.True(Named<Button>(window, "TodoUndoClear").IsVisible);
    }

    [AvaloniaFact]
    public void UndoClear_PutsTheItemsBackAndGoesAway()
    {
        var window = OpenWith("- [x] bread\n- [ ] milk");
        Named<Button>(window, "TodoClear").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Named<Button>(window, "TodoUndoClear").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("- [x] bread\n- [ ] milk", NoteOf(window).Content);
        Assert.False(Named<Button>(window, "TodoUndoClear").IsVisible);
    }

    /// <summary>Nothing ticked means nothing to clear, so the button is not offered.</summary>
    [AvaloniaFact]
    public void ClearCompleted_WithNothingTicked_IsNotOffered()
    {
        var window = OpenWith("- [ ] milk");

        Assert.False(Named<Button>(window, "TodoClear").IsVisible);
    }
}
