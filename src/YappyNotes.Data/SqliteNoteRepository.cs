using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using YappyNotes.Core;

namespace YappyNotes.Data;

/// <summary>
/// Notes, in SQLite.
/// </summary>
public sealed class SqliteNoteRepository : INoteRepository
{
    /// <summary>
    /// Timestamps are ISO-8601 in UTC with a Z, seven decimal places: SQLite has
    /// no date type, and this sorts correctly as text and stays readable to
    /// anyone who opens the file in a SQL browser.
    /// </summary>
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    /// <summary>
    /// Ordered by Id, which is a UUIDv7 and therefore sorts by creation time.
    /// That is why there is no index on CreatedUtc: the primary key already is one.
    /// </summary>
    private const string SelectColumns =
        """
        select n.Id, n.Title, n.Content, n.Color, n.X, n.Y, n.Width, n.Height,
               n.IsAlwaysOnTop, n.IsArchived, n.CreatedUtc, n.ModifiedUtc,
               t.Direction, t.Duration, t.Label, t.StartedAtUtc, t.Accumulated
        from notes n
        left join note_timers t on t.NoteId = n.Id
        """;

    private readonly NoteDatabase _database;

    public SqliteNoteRepository(NoteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} order by n.Id;";

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<Note?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} where n.Id = @id;";
        command.Parameters.AddWithValue("@id", ToStorage(id));

        var notes = await ReadAllAsync(command, cancellationToken);
        return notes.Count == 0 ? null : notes[0];
    }

    public async Task<IReadOnlyList<Note>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return await GetAllAsync(cancellationToken);
        }

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            {SelectColumns}
            where Title like @pattern escape '\' or Content like @pattern escape '\'
            order by Id;
            """;
        command.Parameters.AddWithValue("@pattern", $"%{EscapeForLike(text)}%");

        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task InsertAsync(Note note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            insert into notes (Id, Title, Content, Color, X, Y, Width, Height,
                               IsAlwaysOnTop, IsArchived, CreatedUtc, ModifiedUtc)
            values (@id, @title, @content, @color, @x, @y, @width, @height,
                    @isAlwaysOnTop, @isArchived, @createdUtc, @modifiedUtc);
            """;
        AddAllParameters(command, note);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException failure) when (failure.SqliteErrorCode == 19)
        {
            // SQLITE_CONSTRAINT. Translated so that callers - and the shared
            // repository contract - see the same exception whichever
            // implementation they are talking to.
            throw new InvalidOperationException($"A note with id {note.Id} is already stored.", failure);
        }

        await SaveTimerAsync(connection, note, cancellationToken);
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            update notes set
                Title = @title, Content = @content, Color = @color,
                X = @x, Y = @y, Width = @width, Height = @height,
                IsAlwaysOnTop = @isAlwaysOnTop, IsArchived = @isArchived,
                ModifiedUtc = @modifiedUtc
            where Id = @id;
            """;
        AddAllParameters(command, note);

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new KeyNotFoundException($"There is no note with id {note.Id}.");
        }

        await SaveTimerAsync(connection, note, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from notes where Id = @id;";
        command.Parameters.AddWithValue("@id", ToStorage(id));

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new KeyNotFoundException($"There is no note with id {id}.");
        }
    }

    private static async Task<IReadOnlyList<Note>> ReadAllAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var notes = new List<Note>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notes.Add(Read(reader));
        }

        return notes;
    }

    private static Note Read(SqliteDataReader reader) => new()
    {
        Timer = ReadTimer(reader),
        Id = Guid.Parse(reader.GetString(0)),
        Title = reader.GetString(1),
        Content = reader.GetString(2),
        Color = Enum.Parse<NoteColor>(reader.GetString(3)),
        X = reader.GetInt32(4),
        Y = reader.GetInt32(5),
        Width = reader.GetInt32(6),
        Height = reader.GetInt32(7),
        IsAlwaysOnTop = reader.GetBoolean(8),
        IsArchived = reader.GetBoolean(9),
        CreatedUtc = ReadTimestamp(reader.GetString(10)),
        ModifiedUtc = ReadTimestamp(reader.GetString(11)),
    };

    /// <summary>The joined note_timers columns, or null where the note has none.</summary>
    private static NoteTimer? ReadTimer(SqliteDataReader reader)
    {
        const int direction = 12;

        if (reader.IsDBNull(direction))
        {
            return null;
        }

        return new NoteTimer
        {
            Direction = Enum.Parse<TimerDirection>(reader.GetString(direction)),
            Duration = TimeSpan.FromTicks(reader.GetInt64(13)),
            Label = reader.GetString(14),
            StartedAtUtc = reader.IsDBNull(15) ? null : ReadTimestamp(reader.GetString(15)),
            Accumulated = TimeSpan.FromTicks(reader.GetInt64(16)),
        };
    }

    /// <summary>
    /// Writes, replaces or removes a note's timer to match the note handed in.
    /// Always paired with the note's own write and inside the same connection.
    /// </summary>
    private static async Task SaveTimerAsync(
        SqliteConnection connection, Note note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        if (note.Timer is not { } timer)
        {
            command.CommandText = "delete from note_timers where NoteId = @id;";
            command.Parameters.AddWithValue("@id", ToStorage(note.Id));
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        // One statement rather than a read-then-write, for the same reason the
        // settings store uses one.
        command.CommandText =
            """
            insert into note_timers (NoteId, Direction, Duration, Label, StartedAtUtc, Accumulated)
            values (@id, @direction, @duration, @label, @startedAt, @accumulated)
            on conflict (NoteId) do update set
                Direction = excluded.Direction,
                Duration = excluded.Duration,
                Label = excluded.Label,
                StartedAtUtc = excluded.StartedAtUtc,
                Accumulated = excluded.Accumulated;
            """;
        command.Parameters.AddWithValue("@id", ToStorage(note.Id));
        command.Parameters.AddWithValue("@direction", timer.Direction.ToString());
        command.Parameters.AddWithValue("@duration", timer.Duration.Ticks);
        command.Parameters.AddWithValue("@label", timer.Label);
        command.Parameters.AddWithValue(
            "@startedAt",
            timer.StartedAtUtc is { } startedAt ? ToStorage(startedAt) : DBNull.Value);
        command.Parameters.AddWithValue("@accumulated", timer.Accumulated.Ticks);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddAllParameters(SqliteCommand command, Note note)
    {
        command.Parameters.AddWithValue("@id", ToStorage(note.Id));
        command.Parameters.AddWithValue("@title", note.Title);
        command.Parameters.AddWithValue("@content", note.Content);
        // By name, so that inserting a colour into the middle of the enum later
        // does not repaint every note already written.
        command.Parameters.AddWithValue("@color", note.Color.ToString());
        command.Parameters.AddWithValue("@x", note.X);
        command.Parameters.AddWithValue("@y", note.Y);
        command.Parameters.AddWithValue("@width", note.Width);
        command.Parameters.AddWithValue("@height", note.Height);
        command.Parameters.AddWithValue("@isAlwaysOnTop", note.IsAlwaysOnTop);
        command.Parameters.AddWithValue("@isArchived", note.IsArchived);
        command.Parameters.AddWithValue("@createdUtc", ToStorage(note.CreatedUtc));
        command.Parameters.AddWithValue("@modifiedUtc", ToStorage(note.ModifiedUtc));
    }

    /// <summary>
    /// Lowercase hyphenated hex, always. Mixing two spellings of a guid in one
    /// text column breaks both the primary key and the ordering, and every row
    /// already written stays wrong.
    /// </summary>
    private static string ToStorage(Guid id) => id.ToString("d");

    private static string ToStorage(DateTimeOffset moment)
        => moment.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ReadTimestamp(string stored)
        => DateTimeOffset.ParseExact(
            stored, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    /// <summary>
    /// Underscore and percent are LIKE wildcards. A reader searching for "100%"
    /// means the character, so the pattern escapes them - and the backslash
    /// itself first, or escaping would corrupt a search for a backslash.
    /// </summary>
    private static string EscapeForLike(string text)
        => text.Replace(@"\", @"\\", StringComparison.Ordinal)
               .Replace("%", @"\%", StringComparison.Ordinal)
               .Replace("_", @"\_", StringComparison.Ordinal);
}
