using Microsoft.Data.Sqlite;
using SmartNotes.Data;

namespace SmartNotes.Data.Tests;

public sealed class MigratorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"smartnotes-migrator-{Guid.CreateVersion7()}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private NoteDatabase NewDatabase()
    {
        Directory.CreateDirectory(_directory);
        return new NoteDatabase(Path.Combine(_directory, $"{Guid.CreateVersion7()}.db"));
    }

    private static async Task<object?> ScalarAsync(NoteDatabase database, string sql)
    {
        await using var connection = await database.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(Token);
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_CreatesTheNotesTable()
    {
        var database = NewDatabase();

        await new Migrator().MigrateAsync(database, Token);

        Assert.Equal(1L, await ScalarAsync(database,
            "select count(*) from sqlite_master where type = 'table' and name = 'notes';"));
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_CreatesTheSettingsTable()
    {
        var database = NewDatabase();

        await new Migrator().MigrateAsync(database, Token);

        Assert.Equal(1L, await ScalarAsync(database,
            "select count(*) from sqlite_master where type = 'table' and name = 'settings';"));
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_RecordsTheSchemaVersionItReached()
    {
        var database = NewDatabase();
        var migrator = new Migrator();

        await migrator.MigrateAsync(database, Token);

        Assert.Equal((long)migrator.LatestVersion, await ScalarAsync(database, "pragma user_version;"));
        Assert.True(migrator.LatestVersion > 0);
    }

    [Fact]
    public async Task MigrateAsync_OnADatabaseThatIsAlreadyCurrent_LeavesTheNotesAlone()
    {
        // The app migrates on every start, so this is the ordinary path, not an
        // edge case. A step that ran twice would take the notes with it.
        var database = NewDatabase();
        await new Migrator().MigrateAsync(database, Token);

        await using (var connection = await database.OpenAsync(Token))
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                insert into notes (Id, Title, Content, Color, X, Y, Width, Height,
                                   IsAlwaysOnTop, IsArchived, CreatedUtc, ModifiedUtc)
                values ('an-id', 'kept', '', 'Yellow', 0, 0, 280, 300, 0, 0, '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z');
                """;
            await insert.ExecuteNonQueryAsync(Token);
        }

        await new Migrator().MigrateAsync(database, Token);

        Assert.Equal(1L, await ScalarAsync(database, "select count(*) from notes;"));
        Assert.Equal("kept", await ScalarAsync(database, "select Title from notes;"));
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_TurnsOnWriteAheadLogging()
    {
        // Two note windows saving at once is the normal case here, and the
        // default rollback journal makes one of them wait on the other.
        var database = NewDatabase();

        await new Migrator().MigrateAsync(database, Token);

        Assert.Equal("wal", await ScalarAsync(database, "pragma journal_mode;"));
    }

    [Fact]
    public async Task MigrateAsync_WhenTheFolderDoesNotExistYet_IsNotTheMigratorsProblem()
    {
        // First run on a new machine: something has to create the directory, and
        // it is UserPaths.EnsureCreated rather than this. Written down because
        // the failure is otherwise a puzzling "unable to open database file".
        var missing = Path.Combine(_directory, "not-made-yet", "notes.db");
        var database = new NoteDatabase(missing);

        await Assert.ThrowsAsync<SqliteException>(() => new Migrator().MigrateAsync(database, Token));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
