using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

/// <summary>The manager's to-do view, wired to its controls.</summary>
public class ManagerTodoWindowTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly FakeWindowManager _windows;

    public ManagerTodoWindowTests()
    {
        _notes = new NoteService(_repository, _clock);
        _windows = new FakeWindowManager { Notes = _notes };
    }

    private async Task<Note> SeedAsync(string title, string content)
    {
        var note = await _notes.CreateAsync();
        note.Title = title;
        note.Content = content;
        await _notes.SaveAsync(note);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return note;
    }

    private static async Task SettledAsync(ManagerViewModel manager)
    {
        if (manager.SearchCommand.ExecutionTask is { } running)
        {
            await running;
        }

        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(ManagerWindow Window, ManagerViewModel Manager)> OpenOnTodosAsync()
    {
        var manager = new ManagerViewModel(_notes, _windows, _clock);
        var window = new ManagerWindow();
        window.Bind(manager);
        window.Show();
        await SettledAsync(manager);

        window.FindControl<ToggleButton>("TodosToggle")!.IsChecked = true;
        await SettledAsync(manager);
        return (window, manager);
    }

    private static List<CheckBox> TodoBoxes(ManagerWindow window)
        => [.. window.FindControl<ItemsControl>("TodoList")!.GetVisualDescendants().OfType<CheckBox>()];

    [AvaloniaFact]
    public async Task TodosToggle_WhenTurnedOn_ShowsTheTodosInsteadOfTheNotes()
    {
        await SeedAsync("Shopping", "- [ ] milk\n- [ ] eggs");

        var (window, _) = await OpenOnTodosAsync();

        Assert.True(window.FindControl<ItemsControl>("TodoList")!.IsVisible);
        Assert.False(window.FindControl<ListBox>("NoteList")!.IsVisible);
        Assert.Equal(["milk", "eggs"], TodoBoxes(window).Select(box => box.Content as string));
    }

    [AvaloniaFact]
    public async Task ClickingATodosBox_TicksItInItsNote()
    {
        var shopping = await SeedAsync("Shopping", "- [ ] milk\n- [ ] eggs");
        var (window, manager) = await OpenOnTodosAsync();
        var milk = TodoBoxes(window)[0];
        var centre = milk.TranslatePoint(new Point(8, milk.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        if (manager.TickTodoCommand.ExecutionTask is { } ticking)
        {
            await ticking;
        }

        Dispatcher.UIThread.RunJobs();

        Assert.Equal("- [x] milk\n- [ ] eggs", (await _repository.GetByIdAsync(shopping.Id))!.Content);
        Assert.Equal(["eggs"], TodoBoxes(window).Select(box => box.Content as string));
    }

    [AvaloniaFact]
    public async Task StatusBar_OnTheTodos_CountsThemAndTheirNotes()
    {
        await SeedAsync("Shopping", "- [ ] milk\n- [ ] eggs");
        await SeedAsync("Stream", "- [ ] scene switcher");

        var (window, _) = await OpenOnTodosAsync();

        Assert.Equal("3 to-dos in 2 notes", window.FindControl<TextBlock>("StatusCount")!.Text);
    }

    /// <summary>
    /// A group is a scrap of the note's paper, so it has to resolve light in a dark
    /// manager - the same trap the note rows fell into.
    /// </summary>
    [AvaloniaFact]
    public async Task TodoGroups_WithTheManagerInDarkMode_StillResolveLight()
    {
        await SeedAsync("Shopping", "- [ ] milk");
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        try
        {
            var (window, _) = await OpenOnTodosAsync();

            var scope = TodoBoxes(window)[0].GetVisualAncestors().OfType<ThemeVariantScope>().FirstOrDefault();

            Assert.True(scope is not null, "the to-do groups are not inside a ThemeVariantScope");
            Assert.Equal(ThemeVariant.Light, scope!.ActualThemeVariant);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaFact]
    public async Task TodoList_ForATodoDueToday_SaysSoInTheTodayColour()
    {
        await SeedAsync("Week", "- [ ] rent @2026-09-17");

        var (window, _) = await OpenOnTodosAsync();

        var due = window.FindControl<ItemsControl>("TodoList")!.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.Name == "TodoDue");
        Assert.Equal("Today", due.Text);
        Assert.Equal(DueBrushes.For(DueState.Today), due.Foreground);
    }
}
