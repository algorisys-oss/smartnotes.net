using Microsoft.Extensions.Time.Testing;
using SmartNotes.Core;
using SmartNotes.TestKit;

namespace SmartNotes.Core.Tests;

public class AutoSaveServiceTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly InMemoryNoteRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private NoteService NewNoteService() => new(_repository, _clock);

    private AutoSaveService NewAutoSave(NoteService notes) => new(notes, _clock, Debounce);

    private async Task<string?> StoredContentAsync(Note note)
        => (await _repository.GetByIdAsync(note.Id, Token))?.Content;

    [Fact]
    public async Task Schedule_BeforeTheDebounceIsUp_HasNotWrittenAnything()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        await using var autoSave = NewAutoSave(notes);

        note.Content = "typing";
        autoSave.Schedule(note);
        _clock.Advance(Debounce - TimeSpan.FromMilliseconds(1));
        await autoSave.WhenIdleAsync();

        Assert.Equal(string.Empty, await StoredContentAsync(note));
    }

    [Fact]
    public async Task Schedule_OnceTheDebounceIsUp_WritesWithoutBeingAsked()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        await using var autoSave = NewAutoSave(notes);

        note.Content = "typing";
        autoSave.Schedule(note);
        _clock.Advance(Debounce);
        await autoSave.WhenIdleAsync();

        Assert.Equal("typing", await StoredContentAsync(note));
    }

    /// <summary>
    /// The point of a debounce. Someone typing a sentence should produce one
    /// write, not one per keystroke.
    /// </summary>
    [Fact]
    public async Task Schedule_AgainWhileWaiting_PutsTheWriteOffRatherThanAddingOne()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        var counting = new CountingNoteRepository(_repository);
        var countedNotes = new NoteService(counting, _clock);
        await using var autoSave = new AutoSaveService(countedNotes, _clock, Debounce);

        for (var keystroke = 0; keystroke < 10; keystroke++)
        {
            note.Content += "a";
            autoSave.Schedule(note);
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            await autoSave.WhenIdleAsync();
        }

        Assert.Equal(0, counting.Updates);

        _clock.Advance(Debounce);
        await autoSave.WhenIdleAsync();

        Assert.Equal(1, counting.Updates);
        Assert.Equal("aaaaaaaaaa", await StoredContentAsync(note));
    }

    [Fact]
    public async Task Schedule_WhenTheDebouncePasses_WritesWhatTheNoteSaysThenRatherThanWhenItWasScheduled()
    {
        // The window keeps editing the same object after scheduling, and what
        // belongs on disk is the latest text, not a snapshot of the first keystroke.
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        await using var autoSave = NewAutoSave(notes);

        note.Content = "first";
        autoSave.Schedule(note);
        note.Content = "second";
        _clock.Advance(Debounce);
        await autoSave.WhenIdleAsync();

        Assert.Equal("second", await StoredContentAsync(note));
    }

    [Fact]
    public async Task Schedule_AfterAWriteHasHappened_DoesNotWriteAgainOnItsOwn()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        var counting = new CountingNoteRepository(_repository);
        await using var autoSave = new AutoSaveService(new NoteService(counting, _clock), _clock, Debounce);

        note.Content = "once";
        autoSave.Schedule(note);
        _clock.Advance(Debounce);
        await autoSave.WhenIdleAsync();

        _clock.Advance(TimeSpan.FromMinutes(10));
        await autoSave.WhenIdleAsync();

        Assert.Equal(1, counting.Updates);
    }

    /// <summary>
    /// A note closed mid-debounce must still be saved. Losing the last sentence
    /// someone typed because they closed the window too quickly is exactly the
    /// bug autosave exists to prevent.
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithAWritePending_WritesImmediately()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        await using var autoSave = NewAutoSave(notes);

        note.Content = "closed in a hurry";
        autoSave.Schedule(note);
        await autoSave.FlushAsync(note.Id);

        Assert.Equal("closed in a hurry", await StoredContentAsync(note));
    }

    [Fact]
    public async Task FlushAsync_ForANoteWithNothingPending_DoesNothingAndDoesNotThrow()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        var counting = new CountingNoteRepository(_repository);
        await using var autoSave = new AutoSaveService(new NoteService(counting, _clock), _clock, Debounce);

        await autoSave.FlushAsync(note.Id);

        Assert.Equal(0, counting.Updates);
    }

    [Fact]
    public async Task FlushAsync_HavingWritten_LeavesNothingForTheDebounceToDo()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        var counting = new CountingNoteRepository(_repository);
        await using var autoSave = new AutoSaveService(new NoteService(counting, _clock), _clock, Debounce);

        note.Content = "flushed";
        autoSave.Schedule(note);
        await autoSave.FlushAsync(note.Id);
        _clock.Advance(Debounce * 2);
        await autoSave.WhenIdleAsync();

        Assert.Equal(1, counting.Updates);
    }

    [Fact]
    public async Task FlushAllAsync_WithSeveralNotesPending_WritesEveryOne()
    {
        // App shutdown.
        var notes = NewNoteService();
        var first = await notes.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        var second = await notes.CreateAsync(Token);
        await using var autoSave = NewAutoSave(notes);

        first.Content = "one";
        second.Content = "two";
        autoSave.Schedule(first);
        autoSave.Schedule(second);
        await autoSave.FlushAllAsync();

        Assert.Equal("one", await StoredContentAsync(first));
        Assert.Equal("two", await StoredContentAsync(second));
    }

    [Fact]
    public async Task DisposeAsync_WithAWritePending_WritesItRatherThanDroppingIt()
    {
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        var autoSave = NewAutoSave(notes);

        note.Content = "not lost on the way out";
        autoSave.Schedule(note);
        await autoSave.DisposeAsync();

        Assert.Equal("not lost on the way out", await StoredContentAsync(note));
    }

    [Fact]
    public async Task Schedule_ForTwoDifferentNotes_DebouncesThemSeparately()
    {
        var notes = NewNoteService();
        var typedIn = await notes.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        var leftAlone = await notes.CreateAsync(Token);
        await using var autoSave = NewAutoSave(notes);

        leftAlone.Content = "scheduled first";
        autoSave.Schedule(leftAlone);
        _clock.Advance(TimeSpan.FromMilliseconds(400));

        typedIn.Content = "scheduled later";
        autoSave.Schedule(typedIn);
        _clock.Advance(TimeSpan.FromMilliseconds(400));
        await autoSave.WhenIdleAsync();

        // The first note's own 750ms is up; the second's is not.
        Assert.Equal("scheduled first", await StoredContentAsync(leftAlone));
        Assert.Equal(string.Empty, await StoredContentAsync(typedIn));
    }

    [Fact]
    public async Task Schedule_ForANoteThatWasPurged_DoesNotBringDownTheApp()
    {
        // A window can still be closing while the note behind it is gone.
        var notes = NewNoteService();
        var note = await notes.CreateAsync(Token);
        await notes.ArchiveAsync(note.Id, Token);
        await notes.PurgeAsync(note.Id, Token);
        await using var autoSave = NewAutoSave(notes);

        note.Content = "writing to a ghost";
        autoSave.Schedule(note);
        _clock.Advance(Debounce);

        await autoSave.WhenIdleAsync();
    }
}
