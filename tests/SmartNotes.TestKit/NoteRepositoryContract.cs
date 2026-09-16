using Microsoft.Extensions.Time.Testing;
using SmartNotes.Core;

namespace SmartNotes.TestKit;

/// <summary>
/// What every INoteRepository must do, whatever it stores notes in.
/// </summary>
/// <remarks>
/// Derived once per implementation - the in-memory fake in Core.Tests, SQLite in
/// Data.Tests - so the two cannot drift. The fake is what the service and
/// view-model tests run against, and a fake that behaves differently from the
/// real store is worse than no fake at all: it makes those tests pass for
/// reasons that will not hold in the app.
/// </remarks>
public abstract class NoteRepositoryContract
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    protected readonly FakeTimeProvider Clock = new(Noon);

    /// <summary>A repository with nothing in it.</summary>
    protected abstract Task<INoteRepository> NewRepositoryAsync();

    private Note NewNote(string title = "", string content = "")
    {
        var note = Note.Create(Clock);
        note.Title = title;
        note.Content = content;
        Clock.Advance(TimeSpan.FromMilliseconds(5));
        return note;
    }

    [Fact]
    public async Task GetAllAsync_OnAnEmptyRepository_ReturnsNothing()
    {
        var repository = await NewRepositoryAsync();

        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ForAnIdThatWasNeverInserted_ReturnsNull()
    {
        var repository = await NewRepositoryAsync();

        Assert.Null(await repository.GetByIdAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task InsertAsync_ThenGetById_ReturnsEveryFieldUnchanged()
    {
        var repository = await NewRepositoryAsync();
        var note = NewNote("Shopping", "milk\nbread");
        note.Color = NoteColor.Blue;
        note.X = 120;
        note.Y = 240;
        note.Width = 400;
        note.Height = 500;
        note.IsAlwaysOnTop = true;

        await repository.InsertAsync(note);
        var stored = await repository.GetByIdAsync(note.Id);

        Assert.NotNull(stored);
        Assert.Equal(note.Id, stored.Id);
        Assert.Equal("Shopping", stored.Title);
        Assert.Equal("milk\nbread", stored.Content);
        Assert.Equal(NoteColor.Blue, stored.Color);
        Assert.Equal(120, stored.X);
        Assert.Equal(240, stored.Y);
        Assert.Equal(400, stored.Width);
        Assert.Equal(500, stored.Height);
        Assert.True(stored.IsAlwaysOnTop);
        Assert.False(stored.IsArchived);
        Assert.Equal(note.CreatedUtc, stored.CreatedUtc);
        Assert.Equal(note.ModifiedUtc, stored.ModifiedUtc);
    }

    [Fact]
    public async Task InsertAsync_AnIdAlreadyStored_Throws()
    {
        var repository = await NewRepositoryAsync();
        var note = NewNote("first");
        await repository.InsertAsync(note);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InsertAsync(note));
    }

    /// <summary>
    /// The id sorts by creation time, so this is age order without a second
    /// column to keep in step.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WithSeveralNotes_ReturnsThemOldestFirst()
    {
        var repository = await NewRepositoryAsync();
        var first = NewNote("first");
        var second = NewNote("second");
        var third = NewNote("third");

        await repository.InsertAsync(third);
        await repository.InsertAsync(first);
        await repository.InsertAsync(second);

        var all = await repository.GetAllAsync();

        Assert.Equal(["first", "second", "third"], all.Select(n => n.Title));
    }

    [Fact]
    public async Task GetAllAsync_WithAnArchivedNote_StillReturnsIt()
    {
        // The repository does not filter; deciding what to show is the service's
        // job. Keeping that rule in one place is why it is asserted here.
        var repository = await NewRepositoryAsync();
        var note = NewNote("archived");
        note.IsArchived = true;
        await repository.InsertAsync(note);

        var all = await repository.GetAllAsync();

        Assert.Single(all);
        Assert.True(all[0].IsArchived);
    }

    [Fact]
    public async Task UpdateAsync_OnAStoredNote_ReplacesEveryMutableField()
    {
        var repository = await NewRepositoryAsync();
        var note = NewNote("before", "old body");
        await repository.InsertAsync(note);

        note.Title = "after";
        note.Content = "new body";
        note.Color = NoteColor.Pink;
        note.X = 7;
        note.IsArchived = true;
        note.ModifiedUtc = Clock.GetUtcNow();
        await repository.UpdateAsync(note);

        var stored = await repository.GetByIdAsync(note.Id);

        Assert.NotNull(stored);
        Assert.Equal("after", stored.Title);
        Assert.Equal("new body", stored.Content);
        Assert.Equal(NoteColor.Pink, stored.Color);
        Assert.Equal(7, stored.X);
        Assert.True(stored.IsArchived);
        Assert.Equal(note.ModifiedUtc, stored.ModifiedUtc);
    }

    [Fact]
    public async Task UpdateAsync_ForAnIdThatWasNeverInserted_Throws()
    {
        var repository = await NewRepositoryAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repository.UpdateAsync(NewNote("ghost")));
    }

    [Fact]
    public async Task DeleteAsync_OnAStoredNote_RemovesIt()
    {
        var repository = await NewRepositoryAsync();
        var note = NewNote("doomed");
        await repository.InsertAsync(note);

        await repository.DeleteAsync(note.Id);

        Assert.Null(await repository.GetByIdAsync(note.Id));
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_ForAnIdThatWasNeverInserted_Throws()
    {
        var repository = await NewRepositoryAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repository.DeleteAsync(Guid.CreateVersion7()));
    }

    /// <summary>
    /// The one that keeps the fake honest. SQLite round-trips by value, so a
    /// caller holding the note it inserted cannot reach into the database by
    /// mutating it. An in-memory store keyed on the same object reference can,
    /// and would let every test above pass while the app behaved differently.
    /// </summary>
    [Fact]
    public async Task InsertAsync_ThenMutatingTheCallersNote_DoesNotChangeWhatIsStored()
    {
        var repository = await NewRepositoryAsync();
        var note = NewNote("as stored");
        await repository.InsertAsync(note);

        note.Title = "mutated behind the repository's back";

        var stored = await repository.GetByIdAsync(note.Id);
        Assert.NotNull(stored);
        Assert.Equal("as stored", stored.Title);
    }

    [Fact]
    public async Task GetByIdAsync_MutatingWhatItReturned_DoesNotChangeWhatIsStored()
    {
        var repository = await NewRepositoryAsync();
        var note = NewNote("as stored");
        await repository.InsertAsync(note);

        var first = await repository.GetByIdAsync(note.Id);
        first!.Title = "mutated";

        var second = await repository.GetByIdAsync(note.Id);
        Assert.Equal("as stored", second!.Title);
    }

    [Fact]
    public async Task SearchAsync_ForTextInTheTitle_ReturnsThatNote()
    {
        var repository = await NewRepositoryAsync();
        await repository.InsertAsync(NewNote("Shopping list", "milk"));
        await repository.InsertAsync(NewNote("Standup notes", "deploy"));

        var found = await repository.SearchAsync("shopping");

        Assert.Equal(["Shopping list"], found.Select(n => n.Title));
    }

    [Fact]
    public async Task SearchAsync_ForTextInTheBody_ReturnsThatNote()
    {
        var repository = await NewRepositoryAsync();
        await repository.InsertAsync(NewNote("Shopping list", "milk"));
        await repository.InsertAsync(NewNote("Standup notes", "deploy"));

        var found = await repository.SearchAsync("DEPLOY");

        Assert.Equal(["Standup notes"], found.Select(n => n.Title));
    }

    [Fact]
    public async Task SearchAsync_ForTextNobodyWrote_ReturnsNothing()
    {
        var repository = await NewRepositoryAsync();
        await repository.InsertAsync(NewNote("Shopping list", "milk"));

        Assert.Empty(await repository.SearchAsync("kayak"));
    }

    [Fact]
    public async Task SearchAsync_ForBlankText_ReturnsEverything()
    {
        // What a cleared search box means.
        var repository = await NewRepositoryAsync();
        await repository.InsertAsync(NewNote("one"));
        await repository.InsertAsync(NewNote("two"));

        Assert.Equal(2, (await repository.SearchAsync("   ")).Count);
    }

    /// <summary>
    /// Underscore and percent are LIKE wildcards. A reader typing one means the
    /// character, not "any character", and the SQLite implementation is the one
    /// that has to escape it - which is exactly why the rule is in the contract
    /// rather than in that implementation's own tests.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ForTextHoldingALikeWildcard_TreatsItAsLiteral()
    {
        var repository = await NewRepositoryAsync();
        await repository.InsertAsync(NewNote("100% done"));
        await repository.InsertAsync(NewNote("nothing like it"));

        var found = await repository.SearchAsync("100%");

        Assert.Equal(["100% done"], found.Select(n => n.Title));
    }

    [Fact]
    public async Task SearchAsync_WithSeveralMatches_ReturnsThemOldestFirst()
    {
        var repository = await NewRepositoryAsync();
        var first = NewNote("note one");
        var second = NewNote("note two");
        await repository.InsertAsync(second);
        await repository.InsertAsync(first);

        var found = await repository.SearchAsync("note");

        Assert.Equal(["note one", "note two"], found.Select(n => n.Title));
    }
}
