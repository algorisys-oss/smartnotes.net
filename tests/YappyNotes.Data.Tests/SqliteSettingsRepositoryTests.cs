using Microsoft.Data.Sqlite;
using YappyNotes.Core;
using YappyNotes.Data;
using YappyNotes.TestKit;

namespace YappyNotes.Data.Tests;

/// <summary>SQLite, against the same contract the in-memory settings store answers.</summary>
public sealed class SqliteSettingsRepositoryTests : SettingsRepositoryContract, IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"yappynotes-settings-{Guid.CreateVersion7()}");

    protected override async Task<ISettingsRepository> NewRepositoryAsync()
    {
        Directory.CreateDirectory(_directory);
        var database = new NoteDatabase(Path.Combine(_directory, $"{Guid.CreateVersion7()}.db"));
        await new Migrator().MigrateAsync(database, TestContext.Current.CancellationToken);

        return new SqliteSettingsRepository(database);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
