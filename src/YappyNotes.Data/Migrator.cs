using Microsoft.Data.Sqlite;

namespace YappyNotes.Data;

/// <summary>
/// Brings a database up to the schema this build expects.
/// </summary>
/// <remarks>
/// <para>
/// `PRAGMA user_version` holds the number of steps already applied. Migrating
/// runs the ones above it, in order, each in a transaction, and records the new
/// number in the same transaction - so an interrupted run leaves the database at
/// a version it really is.
/// </para>
/// <para>
/// <b>Steps are append-only and never edited once released.</b> Editing one
/// leaves every database that already ran it in a state no code path can reach,
/// on a machine you cannot look at. Add another step instead.
/// </para>
/// </remarks>
public sealed class Migrator
{
    private static readonly string[] Steps =
    [
        // 1 - the initial schema.
        """
        create table notes (
            Id            text    not null primary key,
            Title         text    not null,
            Content       text    not null,
            Color         text    not null,
            X             integer not null,
            Y             integer not null,
            Width         integer not null,
            Height        integer not null,
            IsAlwaysOnTop integer not null,
            IsArchived    integer not null,
            CreatedUtc    text    not null,
            ModifiedUtc   text    not null
        );

        create index ix_notes_archived on notes (IsArchived);

        create table settings (
            Key   text not null primary key,
            Value text not null
        );
        """,

        // 2 - the stream timer. Its own table rather than six nullable columns
        // on notes: most notes have no timer and should not carry the width, and
        // the next dynamic element gets its own table the same way.
        //
        // Nothing in this row changes while the timer runs. What is stored is
        // when the current stretch began and what was banked before it; the
        // number on screen is computed. That is what keeps a ticking note from
        // being dirty on every tick.
        """
        create table note_timers (
            NoteId       text    not null primary key references notes(Id) on delete cascade,
            Direction    text    not null,
            Duration     integer not null,
            Label        text    not null,
            StartedAtUtc text    null,
            Accumulated  integer not null
        );
        """,
    ];

    /// <summary>The schema version this build expects.</summary>
    public int LatestVersion => Steps.Length;

    public async Task MigrateAsync(NoteDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        await using var connection = await database.OpenAsync(cancellationToken);

        // Write-ahead logging is a property of the file, so it only has to be set
        // once - but it cannot be set inside a transaction, which is why it is
        // here rather than in a step.
        await ExecuteAsync(connection, "pragma journal_mode = wal;", cancellationToken);

        var current = await CurrentVersionAsync(connection, cancellationToken);

        for (var version = current; version < Steps.Length; version++)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await ExecuteAsync(connection, Steps[version], cancellationToken, transaction);

            // Interpolated rather than parameterised because PRAGMA does not take
            // parameters. The value is a loop counter, never anything a reader typed.
            await ExecuteAsync(connection, $"pragma user_version = {version + 1};", cancellationToken, transaction);

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task<int> CurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "pragma user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        System.Data.Common.DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction is SqliteTransaction sqliteTransaction)
        {
            command.Transaction = sqliteTransaction;
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
