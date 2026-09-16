using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.Core;
using YappyNotes.TestKit;

namespace YappyNotes.App.Tests;

/// <summary>
/// One note, one view-model, one window - however many places ask for it.
/// </summary>
public class WindowManagerIdentityTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;
    private readonly WindowManager _windows;

    public WindowManagerIdentityTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
        _windows = new WindowManager(_notes, _autoSave);
    }

    /// <summary>
    /// The manager and the desktop both want to open notes. If each built its
    /// own view-model there would be two Note objects for one note, both being
    /// autosaved, and whichever wrote last would quietly undo the other.
    /// </summary>
    [AvaloniaFact]
    public async Task ShowNoteAsync_ForANoteAlreadyOpen_ReusesTheSameViewModel()
    {
        var note = await _notes.CreateAsync();

        await _windows.ShowNoteAsync(note.Id);
        var first = _windows.ViewModelFor(note.Id);
        await _windows.ShowNoteAsync(note.Id);
        var second = _windows.ViewModelFor(note.Id);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Single(_windows.OpenNotes);
    }

    [AvaloniaFact]
    public async Task ShowNoteAsync_ForANoteThatWasPurged_OpensNothing()
    {
        var note = await _notes.CreateAsync();
        await _notes.ArchiveAsync(note.Id);
        await _notes.PurgeAsync(note.Id);

        await _windows.ShowNoteAsync(note.Id);

        Assert.Empty(_windows.OpenNotes);
    }

    [AvaloniaFact]
    public async Task ShowNoteAsync_AfterItsWindowWasClosed_OpensItAgain()
    {
        var note = await _notes.CreateAsync();
        await _windows.ShowNoteAsync(note.Id);
        _windows.CloseNote(note.Id);

        await _windows.ShowNoteAsync(note.Id);

        Assert.Single(_windows.OpenNotes);
    }
}
