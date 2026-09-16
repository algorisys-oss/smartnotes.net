using Microsoft.Data.Sqlite;
using SmartNotes.Data;

namespace SmartNotes.Data.Tests;

public sealed class MigratorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"smartnotes-migrator-{Guid.CreateVersion7()}");

    private NoteDatabase NewDatabase()
    {
        Directory.CreateDirectory(_directory);
        return new NoteDatabase(Path.Combine(_directory, $"{Guid.CreateVersion7()}.db"));
    }

    private static async Task<object?> ScalarAsync(NoteDatabase factory, string sql)
    {
        await using var connection = await factory.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_CreatesTheNotesTable()
    {
        var factory = NewDatabase();

        await new Migrator().MigrateAsync(factory);

        Assert.Equal(1L, await ScalarAsync(factory,
            "select count(*) from sqlite_master where type = 'table' and name = 'notes';"));
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_CreatesTheSettingsTable()
    {
        var factory = NewDatabase();

        await new Migrator().MigrateAsync(factory);

        Assert.Equal(1L, await ScalarAsync(factory,
            "select count(*) from sqlite_master where type = 'table' and name = 'settings';"));
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_RecordsTheSchemaVersionItReached()
    {
        var factory = NewDatabase();
        var migrator = new Migrator();

        await migrator.MigrateAsync(factory);

        Assert.Equal((long)migrator.LatestVersion, await ScalarAsync(factory, "pragma user_version;"));
        Assert.True(migrator.LatestVersion > 0);
    }

    [Fact]
    public async Task MigrateAsync_OnADatabaseThatIsAlreadyCurrent_LeavesTheNotesAlone()
    {
        // The app migrates on every start, so this is the ordinary path, not an
        // edge case. A step that ran twice would take the notes with it.
        var factory = NewDatabase();
        await new Migrator().MigrateAsync(factory);

        await using (var connection = await factory.OpenAsync())
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                insert into notes (Id, Title, Content, Color, X, Y, Width, Height,
                                   IsAlwaysOnTop, IsArchived, CreatedUtc, ModifiedUtc)
                values ('an-id', 'kept', '', 'Yellow', 0, 0, 280, 300, 0, 0, '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z');
                """;
            await insert.ExecuteNonQueryAsync();
        }

        await new Migrator().MigrateAsync(factory);

        Assert.Equal(1L, await ScalarAsync(factory, "select count(*) from notes;"));
        Assert.Equal("kept", await ScalarAsync(factory, "select Title from notes;"));
    }

    [Fact]
    public async Task MigrateAsync_OnAFreshDatabase_TurnsOnWriteAheadLogging()
    {
        // Two note windows saving at once is the normal case here, and the
        // default rollback journal makes one of them wait on the other.
        var factory = NewDatabase();

        await new Migrator().MigrateAsync(factory);

        Assert.Equal("wal", await ScalarAsync(factory, "pragma journal_mode;"));
    }

    [Fact]
    public async Task MigrateAsync_WhenTheFolderDoesNotExistYet_IsNotTheMigratorsProblem()
    {
        // First run on a new machine: something has to create the directory, and
        // it is UserPaths.EnsureCreated rather than this. Written down because
        // the failure is otherwise a puzzling "unable to open database file".
        var missing = Path.Combine(_directory, "not-made-yet", "notes.db");
        var factory = new NoteDatabase(missing);

        await Assert.ThrowsAsync<SqliteException>(() => new Migrator().MigrateAsync(factory));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
