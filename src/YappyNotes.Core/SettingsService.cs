namespace YappyNotes.Core;

/// <summary>
/// Reads and writes <see cref="AppSettings"/>, and is the only thing that knows
/// what the stored strings mean.
/// </summary>
public sealed class SettingsService
{
    private const string ThemeKey = "theme";
    private const string DefaultNoteColorKey = "defaultNoteColor";
    private const string CheckForUpdatesKey = "checkForUpdates";
    private const string StartAtLoginKey = "startAtLogin";

    private readonly ISettingsRepository _repository;

    public SettingsService(ISettingsRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// The stored settings, with defaults for anything missing or unreadable.
    /// </summary>
    /// <remarks>
    /// Nothing here throws on a value it does not understand. This is read on
    /// every start, and a settings row edited into nonsense would otherwise mean
    /// an app that cannot be opened to fix it.
    /// </remarks>
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAllAsync(cancellationToken);
        var settings = new AppSettings();

        if (TryRead<AppTheme>(stored, ThemeKey, out var theme))
        {
            settings.Theme = theme;
        }

        if (TryRead<NoteColor>(stored, DefaultNoteColorKey, out var color))
        {
            settings.DefaultNoteColor = color;
        }

        if (TryRead(stored, CheckForUpdatesKey, out bool check))
        {
            settings.CheckForUpdates = check;
        }

        if (TryRead(stored, StartAtLoginKey, out bool startAtLogin))
        {
            settings.StartAtLogin = startAtLogin;
        }

        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _repository.SetAsync(ThemeKey, settings.Theme.ToString(), cancellationToken);
        await _repository.SetAsync(DefaultNoteColorKey, settings.DefaultNoteColor.ToString(), cancellationToken);
        await _repository.SetAsync(CheckForUpdatesKey, settings.CheckForUpdates.ToString(), cancellationToken);
        await _repository.SetAsync(StartAtLoginKey, settings.StartAtLogin.ToString(), cancellationToken);
    }

    private static bool TryRead(IReadOnlyDictionary<string, string> stored, string key, out bool value)
    {
        value = default;
        return stored.TryGetValue(key, out var text) && bool.TryParse(text, out value);
    }

    private static bool TryRead<T>(IReadOnlyDictionary<string, string> stored, string key, out T value)
        where T : struct, Enum
    {
        value = default;
        return stored.TryGetValue(key, out var text) && Enum.TryParse(text, out value);
    }
}
