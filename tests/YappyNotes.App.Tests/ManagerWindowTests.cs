using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

/// <summary>
/// The wiring between the manager window and its view-model. The view-model's own
/// tests cover what search and the archive toggle <i>do</i>; these cover whether
/// the controls are actually connected to them, which is the part that silently
/// stops working.
/// </summary>
public class ManagerWindowTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly FakeWindowManager _windows = new();

    public ManagerWindowTests() => _notes = new NoteService(_repository, _clock);

    private async Task SeedAsync(string title, bool archived = false)
    {
        var note = await _notes.CreateAsync();
        note.Title = title;
        await _notes.SaveAsync(note);
        if (archived)
        {
            await _notes.ArchiveAsync(note.Id);
        }

        _clock.Advance(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// The controls kick the search command off and do not wait for it - which is
    /// right for a UI and useless for an assertion, so the test waits on the
    /// command's own task rather than on a sleep.
    /// </summary>
    private static async Task SettledAsync(ManagerViewModel manager)
    {
        if (manager.SearchCommand.ExecutionTask is { } running)
        {
            await running;
        }
    }

    private (ManagerWindow Window, ManagerViewModel Manager) Open()
    {
        var manager = new ManagerViewModel(_notes, _windows);
        var window = new ManagerWindow();
        window.Bind(manager);
        window.Show();
        return (window, manager);
    }

    [AvaloniaFact]
    public async Task ManagerWindow_WhenOpened_FillsItsListWithoutBeingAsked()
    {
        await SeedAsync("Shopping");

        var (window, manager) = Open();

        Assert.Single(manager.Items);
        Assert.Same(manager, window.DataContext);
    }

    [AvaloniaFact]
    public async Task SearchBox_WhenTypedInto_FiltersTheListAsYouGo()
    {
        await SeedAsync("Shopping");
        await SeedAsync("Standup");
        var (window, manager) = Open();

        window.FindControl<TextBox>("SearchBox")!.Text = "standup";
        await SettledAsync(manager);

        Assert.Equal(["Standup"], manager.Items.Select(i => i.DisplayTitle));
    }

    [AvaloniaFact]
    public async Task ArchiveToggle_WhenTurnedOn_SwitchesTheListToTheArchive()
    {
        await SeedAsync("on the desktop");
        await SeedAsync("filed away", archived: true);
        var (window, manager) = Open();

        window.FindControl<ToggleSwitch>("ArchiveToggle")!.IsChecked = true;
        await SettledAsync(manager);

        Assert.True(manager.ShowingArchive);
        Assert.Equal(["filed away"], manager.Items.Select(i => i.DisplayTitle));
    }

    [AvaloniaFact]
    public async Task NewNoteButton_WhenPressed_MakesANoteAndAsksForItsWindow()
    {
        var (window, manager) = Open();

        await manager.NewNoteCommand.ExecuteAsync(null);

        var created = Assert.Single(await _notes.GetActiveAsync());
        Assert.Equal([created.Id], _windows.Shown);
        Assert.Single(manager.Items);
    }

    [AvaloniaFact]
    public void StatusBar_ShowsWhichBuildThisIs()
    {
        var (window, _) = Open();

        var version = window.FindControl<TextBlock>("StatusVersion")!;

        Assert.False(string.IsNullOrWhiteSpace(version.Text));
        Assert.Matches(@"^\d+\.\d+\.\d+", version.Text!);
        Assert.Equal(AppVersion.Current, version.Text);
    }

    [AvaloniaFact]
    public async Task StatusBar_CountsWhatIsOnTheDesktop()
    {
        await SeedAsync("Shopping");
        await SeedAsync("Standup");

        var (window, _) = Open();

        Assert.Equal("2 notes on the desktop", window.FindControl<TextBlock>("StatusCount")!.Text);
    }

    [AvaloniaFact]
    public async Task StatusBar_WithOneNote_SaysNoteRatherThanNotes()
    {
        await SeedAsync("Shopping");

        var (window, _) = Open();

        Assert.Equal("1 note on the desktop", window.FindControl<TextBlock>("StatusCount")!.Text);
    }

    [AvaloniaFact]
    public async Task StatusBar_FollowsTheListWhenItChanges()
    {
        await SeedAsync("Shopping");
        await SeedAsync("filed away", archived: true);
        var (window, manager) = Open();

        window.FindControl<ToggleSwitch>("ArchiveToggle")!.IsChecked = true;
        await SettledAsync(manager);

        Assert.Equal("1 note archived", window.FindControl<TextBlock>("StatusCount")!.Text);
    }
}
