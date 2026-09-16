using Microsoft.Data.Sqlite;

namespace SmartNotes.Data.Tests;

public class ScaffoldTests
{
    /// <summary>
    /// Microsoft.Data.Sqlite carries a native library, and a native library that
    /// fails to load does so at the first Open() rather than at build time. This
    /// says whether the scaffold is actually able to talk to SQLite on whatever
    /// machine is running it, which is a different question from whether it
    /// compiled.
    /// </summary>
    [Fact]
    public void Scaffold_OpeningADatabase_LoadsTheNativeSqliteLibrary()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select sqlite_version();";
        var version = command.ExecuteScalar() as string;

        Assert.False(string.IsNullOrWhiteSpace(version));
    }
}
