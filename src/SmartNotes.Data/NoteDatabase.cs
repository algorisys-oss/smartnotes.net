using Microsoft.Data.Sqlite;

namespace SmartNotes.Data;

/// <summary>
/// The database file, and how to open a connection to it.
/// </summary>
/// <remarks>
/// A connection per operation rather than one held open: Microsoft.Data.Sqlite
/// pools by connection string, so opening is cheap, and a single shared
/// connection would serialise every note window behind whichever one is writing.
/// Not named SqliteConnectionFactory - Microsoft.Data.Sqlite has an internal type
/// by that name and the collision is baffling when you hit it.
/// </remarks>
public sealed class NoteDatabase
{
    private readonly string _connectionString;

    public NoteDatabase(string databaseFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFile);

        DatabaseFile = databaseFile;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Pooling = true,
        }.ToString();
    }

    public string DatabaseFile { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);

            // Two note windows saving at the same moment is ordinary here rather
            // than rare, and without this the second gets SQLITE_BUSY instead of
            // waiting its turn.
            await using var pragmas = connection.CreateCommand();

            // foreign_keys is per connection rather than per database, and off
            // by default in SQLite itself - though Microsoft.Data.Sqlite turns it
            // on for you, which was checked rather than assumed. Set explicitly
            // so that the cascade taking a note's timer with it does not rest on
            // a provider default that could change under us.
            // TimerCascadeTests asserts the end state either way.
            pragmas.CommandText =
                """
                pragma busy_timeout = 5000;
                pragma foreign_keys = on;
                """;
            await pragmas.ExecuteNonQueryAsync(cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
