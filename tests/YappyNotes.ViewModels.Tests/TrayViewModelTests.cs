using Microsoft.Extensions.Time.Testing;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

/// <summary>
/// The tray menu is what is left to click once every window is shut, so each item
/// has to reach what it names without any window already being open.
/// </summary>
public class TrayViewModelTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly FakeWindowManager _windows = new();
    private readonly FakeAppLifetime _lifetime = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public TrayViewModelTests() => _notes = new NoteService(_repository, _clock);

    private TrayViewModel NewTray() => new(_notes, _windows, _lifetime);

    [Fact]
    public async Task NewNoteCommand_WithNothingOpen_MakesANoteAndOpensIt()
    {
        var tray = NewTray();

        await tray.NewNoteCommand.ExecuteAsync(null);

        var created = Assert.Single(await _notes.GetActiveAsync(Token));
        Assert.Equal([created.Id], _windows.Shown);
    }

    [Fact]
    public async Task ShowAllNotesCommand_WithNotesOnTheDesktop_OpensEveryOne()
    {
        var first = await _notes.CreateAsync(Token);
        var second = await _notes.CreateAsync(Token);
        var tray = NewTray();

        await tray.ShowAllNotesCommand.ExecuteAsync(null);

        // Which notes, not in what order: two ids made in the same millisecond
        // do not sort by creation.
        Assert.Equivalent(new[] { first.Id, second.Id }, _windows.Shown, strict: true);
    }

    /// <summary>
    /// "All notes" means the desktop, not the archive: an archived note coming
    /// back as a window would undo the archive without saying so.
    /// </summary>
    [Fact]
    public async Task ShowAllNotesCommand_WithAnArchivedNote_LeavesItInTheArchive()
    {
        var archived = await _notes.CreateAsync(Token);
        await _notes.ArchiveAsync(archived.Id, Token);
        var tray = NewTray();

        await tray.ShowAllNotesCommand.ExecuteAsync(null);

        Assert.Empty(_windows.Shown);
    }

    /// <summary>
    /// Hiding closes the windows rather than archiving the notes: they are still
    /// on the desktop, and Show all notes - or the next start - brings them back.
    /// </summary>
    [Fact]
    public async Task HideAllNotesCommand_WithNotesOpen_ClosesEveryWindow()
    {
        var first = await _notes.CreateAsync(Token);
        var second = await _notes.CreateAsync(Token);
        await _windows.ShowNoteAsync(first.Id);
        await _windows.ShowNoteAsync(second.Id);
        var tray = NewTray();

        tray.HideAllNotesCommand.Execute(null);

        Assert.Equal([first.Id, second.Id], _windows.Closed);
    }

    [Fact]
    public async Task HideAllNotesCommand_WithNotesOpen_ArchivesNothing()
    {
        var note = await _notes.CreateAsync(Token);
        await _windows.ShowNoteAsync(note.Id);
        var tray = NewTray();

        tray.HideAllNotesCommand.Execute(null);

        Assert.Single(await _notes.GetActiveAsync(Token));
    }

    [Fact]
    public void OpenManagerCommand_WhenInvoked_AsksForTheManagerWindow()
    {
        var tray = NewTray();

        tray.OpenManagerCommand.Execute(null);

        Assert.Equal(1, _windows.ManagerShown);
    }

    [Fact]
    public void QuitCommand_WhenInvoked_AsksTheAppToQuit()
    {
        var tray = NewTray();

        tray.QuitCommand.Execute(null);

        Assert.Equal(1, _lifetime.QuitRequests);
    }

    /// <summary>
    /// Starting the app while it already runs brings the running one forward
    /// rather than opening a second copy, and "forward" means what a start shows.
    /// </summary>
    [Fact]
    public async Task BringForwardCommand_WhenTheAppIsStartedAgain_OpensTheManager()
    {
        var tray = NewTray();

        await tray.BringForwardCommand.ExecuteAsync(null);

        Assert.Equal(1, _windows.ManagerShown);
    }

    [Fact]
    public async Task BringForwardCommand_WithNotesHidden_ShowsEveryNoteOnTheDesktop()
    {
        var first = await _notes.CreateAsync(Token);
        var second = await _notes.CreateAsync(Token);
        var tray = NewTray();

        await tray.BringForwardCommand.ExecuteAsync(null);

        Assert.Equivalent(new[] { first.Id, second.Id }, _windows.Shown, strict: true);
    }
}
