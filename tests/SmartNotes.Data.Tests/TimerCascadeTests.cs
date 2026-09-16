using Microsoft.Data.Sqlite;
using SmartNotes.Core;
using SmartNotes.Data;

namespace SmartNotes.Data.Tests;

/// <summary>
/// That purging a note takes its timer row with it.
/// </summary>
/// <remarks>
/// This lives here rather than in the shared contract because it is a storage
/// fact that only SQL can see. The contract's version of this question passes
/// either way: re-inserting an id runs the repository's own "this note has no
/// timer, delete any row" path, which tidies up an orphan whether or not the
/// cascade did. Only counting the rows tells the truth.
/// </remarks>
public sealed class TimerCascadeTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"smartnotes-cascade-{Guid.CreateVersion7()}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(NoteDatabase Database, SqliteNoteRepository Repository)> NewAsync()
    {
        Directory.CreateDirectory(_directory);
        var database = new NoteDatabase(Path.Combine(_directory, $"{Guid.CreateVersion7()}.db"));
        await new Migrator().MigrateAsync(database, Token);
        return (database, new SqliteNoteRepository(database));
    }

    private static async Task<long> TimerRowsAsync(NoteDatabase database)
    {
        await using var connection = await database.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from note_timers;";
        return (long)(await command.ExecuteScalarAsync(Token))!;
    }

    [Fact]
    public async Task DeleteAsync_OnANoteWithATimer_LeavesNoOrphanedTimerRow()
    {
        // Ids are never reused, so an orphan is never tidied up by anything else
        // - it just sits in the file for the life of the database.
        var (database, repository) = await NewAsync();
        var note = Note.Create(TimeProvider.System);
        note.Timer = new NoteTimer { Label = "Back in" };
        await repository.InsertAsync(note, Token);
        Assert.Equal(1, await TimerRowsAsync(database));

        await repository.DeleteAsync(note.Id, Token);

        Assert.Equal(0, await TimerRowsAsync(database));
    }

    [Fact]
    public async Task OpenAsync_TurnsForeignKeysOn()
    {
        // Per connection rather than per database. Microsoft.Data.Sqlite happens
        // to turn this on by default, so NoteDatabase setting it explicitly is
        // belt and braces - but this asserts the state the cascade depends on,
        // whoever ends up providing it.
        var (database, _) = await NewAsync();

        await using var connection = await database.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "pragma foreign_keys;";

        Assert.Equal(1L, await command.ExecuteScalarAsync(Token));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
