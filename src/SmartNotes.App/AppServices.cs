using Microsoft.Extensions.DependencyInjection;
using SmartNotes.Core;
using SmartNotes.Data;

namespace SmartNotes.App;

/// <summary>
/// The composition root: opens the database, brings it up to date, and wires the
/// services the windows use.
/// </summary>
/// <remarks>
/// Deliberately free of Avalonia so that starting the application and opening a
/// window are separate things - this part can be tested with a temp directory and
/// no UI at all, which is where the interesting failures are (a missing folder, a
/// migration that did not run).
/// </remarks>
public sealed class AppServices : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private AppServices(ServiceProvider provider, UserPaths paths)
    {
        _provider = provider;
        Paths = paths;
    }

    public UserPaths Paths { get; }

    public NoteService Notes => _provider.GetRequiredService<NoteService>();

    public AutoSaveService AutoSave => _provider.GetRequiredService<AutoSaveService>();

    public SettingsService Settings => _provider.GetRequiredService<SettingsService>();

    /// <summary>
    /// Initialise the database, run migrations, create the services. The first
    /// three steps of the bootstrap; loading notes and restoring their windows
    /// belongs to whoever has a screen.
    /// </summary>
    public static async Task<AppServices> StartAsync(
        UserPaths paths,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // Before anything opens the file: SQLite reports a missing folder as
        // "unable to open database file", which reads like corruption.
        paths.EnsureCreated();

        var database = new NoteDatabase(paths.DatabaseFile);
        await new Migrator().MigrateAsync(database, cancellationToken);

        var services = new ServiceCollection();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton(database);
        services.AddSingleton<INoteRepository, SqliteNoteRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<NoteService>();
        services.AddSingleton<AutoSaveService>();

        return new AppServices(services.BuildServiceProvider(), paths);
    }

    public async ValueTask DisposeAsync()
    {
        // Flushes whatever the debounce was still holding, for every open note.
        await AutoSave.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
