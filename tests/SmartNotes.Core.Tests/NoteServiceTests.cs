using Microsoft.Extensions.Time.Testing;
using SmartNotes.Core;
using SmartNotes.TestKit;

namespace SmartNotes.Core.Tests;

public class NoteServiceTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly InMemoryNoteRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private NoteService NewService() => new(_repository, _clock);

    private async Task<Note> ArchivedNoteAsync(NoteService service, string title)
    {
        var note = await service.CreateAsync(Token);
        note.Title = title;
        await service.SaveAsync(note, Token);
        await service.ArchiveAsync(note.Id, Token);
        return note;
    }

    [Fact]
    public async Task CreateAsync_ReturnsANoteThatIsAlreadyStored()
    {
        var service = NewService();

        var created = await service.CreateAsync(Token);

        var stored = await _repository.GetByIdAsync(created.Id, Token);
        Assert.NotNull(stored);
        Assert.Equal(created.Id, stored.Id);
    }

    [Fact]
    public async Task CreateAsync_TwiceInARow_MakesTwoDistinctNotes()
    {
        var service = NewService();

        var first = await service.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        var second = await service.CreateAsync(Token);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await service.GetActiveAsync(Token)).Count);
    }

    [Fact]
    public async Task SaveAsync_StampsTheMomentItWasSaved()
    {
        var service = NewService();
        var note = await service.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromMinutes(3));

        note.Content = "written later";
        await service.SaveAsync(note, Token);

        var stored = await _repository.GetByIdAsync(note.Id, Token);
        Assert.Equal(Noon.AddMinutes(3), stored!.ModifiedUtc);
    }

    [Fact]
    public async Task SaveAsync_LeavesCreatedUtcWhereItWas()
    {
        var service = NewService();
        var note = await service.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromDays(2));

        await service.SaveAsync(note, Token);

        var stored = await _repository.GetByIdAsync(note.Id, Token);
        Assert.Equal(Noon, stored!.CreatedUtc);
    }

    [Fact]
    public async Task SaveAsync_StampsTheCallersNoteToo()
    {
        // The window keeps the object it is editing, so if only the stored copy
        // were stamped the two would disagree about when it last changed.
        var service = NewService();
        var note = await service.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromMinutes(1));

        await service.SaveAsync(note, Token);

        Assert.Equal(Noon.AddMinutes(1), note.ModifiedUtc);
    }

    [Fact]
    public async Task SaveAsync_ForANoteThatWasNeverCreated_Throws()
    {
        var service = NewService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.SaveAsync(Note.Create(_clock), Token));
    }

    /// <summary>
    /// The one this app cannot get wrong. The delete button archives; nothing a
    /// reader can press in one action destroys a note.
    /// </summary>
    [Fact]
    public async Task ArchiveAsync_MarksTheNoteRatherThanRemovingIt()
    {
        var service = NewService();
        var note = await service.CreateAsync(Token);
        note.Content = "still here";
        await service.SaveAsync(note, Token);

        await service.ArchiveAsync(note.Id, Token);

        var stored = await _repository.GetByIdAsync(note.Id, Token);
        Assert.NotNull(stored);
        Assert.True(stored.IsArchived);
        Assert.Equal("still here", stored.Content);
    }

    [Fact]
    public async Task ArchiveAsync_ForANoteThatWasNeverCreated_Throws()
    {
        var service = NewService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ArchiveAsync(Guid.CreateVersion7(), Token));
    }

    [Fact]
    public async Task RestoreAsync_OnAnArchivedNote_BringsItBack()
    {
        var service = NewService();
        var note = await ArchivedNoteAsync(service, "back from the dead");

        await service.RestoreAsync(note.Id, Token);

        var stored = await _repository.GetByIdAsync(note.Id, Token);
        Assert.False(stored!.IsArchived);
    }

    [Fact]
    public async Task GetActiveAsync_LeavesOutArchivedNotes()
    {
        var service = NewService();
        var kept = await service.CreateAsync(Token);
        kept.Title = "kept";
        await service.SaveAsync(kept, Token);
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        await ArchivedNoteAsync(service, "filed away");

        var active = await service.GetActiveAsync(Token);

        Assert.Equal(["kept"], active.Select(n => n.Title));
    }

    [Fact]
    public async Task GetArchivedAsync_ReturnsOnlyTheArchivedOnes()
    {
        var service = NewService();
        await service.CreateAsync(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        await ArchivedNoteAsync(service, "filed away");

        var archived = await service.GetArchivedAsync(Token);

        Assert.Equal(["filed away"], archived.Select(n => n.Title));
    }

    [Fact]
    public async Task SearchAsync_LeavesOutArchivedNotes()
    {
        // Searching the board should not turn up what you filed away; the
        // archive has its own view.
        var service = NewService();
        var live = await service.CreateAsync(Token);
        live.Title = "milk";
        await service.SaveAsync(live, Token);
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        await ArchivedNoteAsync(service, "milk run, done");

        var found = await service.SearchAsync("milk", Token);

        Assert.Equal(["milk"], found.Select(n => n.Title));
    }

    [Fact]
    public async Task PurgeAsync_OnAnArchivedNote_RemovesItForGood()
    {
        var service = NewService();
        var note = await ArchivedNoteAsync(service, "done with this");

        await service.PurgeAsync(note.Id, Token);

        Assert.Null(await _repository.GetByIdAsync(note.Id, Token));
    }

    /// <summary>
    /// The second half of the archive rule: the only path to a real delete goes
    /// through the archive, so no single action can destroy a note a reader can
    /// still see on their desktop.
    /// </summary>
    [Fact]
    public async Task PurgeAsync_OnANoteThatIsStillLive_RefusesAndKeepsIt()
    {
        var service = NewService();
        var note = await service.CreateAsync(Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PurgeAsync(note.Id, Token));

        Assert.NotNull(await _repository.GetByIdAsync(note.Id, Token));
    }

    [Fact]
    public async Task PurgeAsync_ForANoteThatWasNeverCreated_Throws()
    {
        var service = NewService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.PurgeAsync(Guid.CreateVersion7(), Token));
    }
}
