namespace SmartNotes.Core;

/// <summary>
/// Where SmartNotes keeps the one file it owns.
/// </summary>
/// <remarks>
/// The three platforms disagree about where user data goes, so nothing anywhere
/// else should build a path out of the home directory - do that and the app is
/// wrong on two of the three. Everything goes through here.
/// </remarks>
public sealed class UserPaths
{
    /// <summary>
    /// Overrides the data directory outright. `scripts/dev-start.sh --sandbox`
    /// sets it, so that testing a migration cannot touch the reader's own notes.
    /// </summary>
    public const string DirectoryOverrideVariable = "SMARTNOTES_DATA_DIR";

    public const string DatabaseFileName = "notes.db";

    private const string AppFolderName = "SmartNotes";

    public UserPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = dataDirectory;
        DatabaseFile = Path.Combine(dataDirectory, DatabaseFileName);
    }

    /// <summary>The folder holding the database.</summary>
    public string DataDirectory { get; }

    /// <summary>The database itself. This file is the whole application state.</summary>
    public string DatabaseFile { get; }

    /// <summary>
    /// The paths this machine should use: the override if it is set, the
    /// platform's convention otherwise.
    /// </summary>
    /// <param name="readEnvironmentVariable">
    /// How to read the environment. Defaults to the real one; tests pass their
    /// own rather than mutating the process they run in, which would make them
    /// order-dependent.
    /// </param>
    public static UserPaths Resolve(Func<string, string?>? readEnvironmentVariable = null)
    {
        var read = readEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        var overridden = read(DirectoryOverrideVariable);

        return new UserPaths(
            string.IsNullOrWhiteSpace(overridden) ? DefaultDataDirectory(read) : overridden);
    }

    /// <summary>Makes the data directory if it is not there yet.</summary>
    public void EnsureCreated() => Directory.CreateDirectory(DataDirectory);

    // Written out per platform rather than taken from Environment.SpecialFolder,
    // because the README promises a specific directory on each of the three and
    // SpecialFolder does not map to all of them the way one would guess.
    private static string DefaultDataDirectory(Func<string, string?> read)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", AppFolderName);
        }

        // Linux, and anything else behaving like it.
        var xdgDataHome = read("XDG_DATA_HOME");
        var root = string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.Combine(home, ".local", "share")
            : xdgDataHome;

        return Path.Combine(root, AppFolderName);
    }
}
