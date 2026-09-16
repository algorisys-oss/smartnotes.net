using Microsoft.Data.Sqlite;
using YappyNotes.Core;
using YappyNotes.Data;
using YappyNotes.TestKit;

namespace YappyNotes.Data.Tests;

/// <summary>
/// SQLite, against the same contract the in-memory fake answers in Core.Tests.
/// </summary>
/// <remarks>
/// A real file rather than :memory:, because the app ships against a file and the
/// differences that matter - locking, the journal mode surviving a reconnect, a
/// migration still being there on the next connection - only show up with one.
/// </remarks>
public sealed class SqliteNoteRepositoryTests : NoteRepositoryContract, IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"yappynotes-tests-{Guid.CreateVersion7()}");

    protected override async Task<INoteRepository> NewRepositoryAsync()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, $"{Guid.CreateVersion7()}.db");

        var database = new NoteDatabase(file);
        await new Migrator().MigrateAsync(database, TestContext.Current.CancellationToken);

        return new SqliteNoteRepository(database);
    }

    public void Dispose()
    {
        // The pool keeps the file open, and on Windows that makes the delete fail.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
