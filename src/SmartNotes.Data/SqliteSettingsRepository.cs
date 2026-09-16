using SmartNotes.Core;

namespace SmartNotes.Data;

/// <summary>Settings, in the same SQLite file the notes live in.</summary>
public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly NoteDatabase _database;

    public SqliteSettingsRepository(NoteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>();

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select Key, Value from settings;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return values;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // One statement rather than a read-then-write, so two windows saving a
        // setting at the same moment cannot interleave into a lost update.
        command.CommandText =
            """
            insert into settings (Key, Value) values (@key, @value)
            on conflict (Key) do update set Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
