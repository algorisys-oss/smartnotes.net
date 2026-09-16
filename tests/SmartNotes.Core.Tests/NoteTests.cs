using Microsoft.Extensions.Time.Testing;
using SmartNotes.Core;

namespace SmartNotes.Core.Tests;

public class NoteTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Note_WhenCreated_HasAnId()
    {
        var note = Note.Create(new FakeTimeProvider(Noon));

        Assert.NotEqual(Guid.Empty, note.Id);
    }

    /// <summary>
    /// The id carries its own creation time, so ordering by it is ordering by
    /// age. Milestone 2 onwards leans on that - see docs/plan.md.
    /// </summary>
    [Fact]
    public void Note_CreatedAfterAnother_HasAGreaterId()
    {
        var clock = new FakeTimeProvider(Noon);

        var older = Note.Create(clock);
        clock.Advance(TimeSpan.FromMilliseconds(5));
        var newer = Note.Create(clock);

        Assert.True(
            string.CompareOrdinal(newer.Id.ToString(), older.Id.ToString()) > 0,
            $"ids must sort by creation, but {newer.Id} does not sort after {older.Id}. "
            + "Note.Create must use Guid.CreateVersion7, never Guid.NewGuid.");
    }

    [Fact]
    public void Note_WhenCreated_TakesItsIdTimestampFromTheClock()
    {
        var note = Note.Create(new FakeTimeProvider(Noon));

        // A v7's first 48 bits are Unix milliseconds, big-endian.
        var milliseconds = Convert.ToInt64(note.Id.ToString().Replace("-", "")[..12], 16);

        Assert.Equal(Noon.ToUnixTimeMilliseconds(), milliseconds);
    }

    [Fact]
    public void Note_WhenCreated_StampsCreatedAndModifiedAtTheSameMoment()
    {
        var note = Note.Create(new FakeTimeProvider(Noon));

        Assert.Equal(Noon, note.CreatedUtc);
        Assert.Equal(Noon, note.ModifiedUtc);
    }

    [Fact]
    public void Note_WhenCreated_IsEmptyAndUnarchivedAndNotPinned()
    {
        var note = Note.Create(new FakeTimeProvider(Noon));

        Assert.Equal(string.Empty, note.Title);
        Assert.Equal(string.Empty, note.Content);
        Assert.False(note.IsArchived);
        Assert.False(note.IsAlwaysOnTop);
    }

    [Fact]
    public void Note_WhenCreated_IsBigEnoughToTypeIn()
    {
        var note = Note.Create(new FakeTimeProvider(Noon));

        Assert.True(note.Width > 0 && note.Height > 0);
    }

    [Fact]
    public void Note_WhenCreated_IsTheDefaultColour()
    {
        var note = Note.Create(new FakeTimeProvider(Noon));

        Assert.Equal(NoteColor.Yellow, note.Color);
    }
}
