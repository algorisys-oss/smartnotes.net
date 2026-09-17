using CommunityToolkit.Mvvm.ComponentModel;
using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>
/// The settings window.
/// </summary>
/// <remarks>
/// Changing a setting saves it, immediately and without a button - the same rule
/// the notes follow. A theme is applied as well as saved, because picking one and
/// having to restart to see it is what makes a settings window feel broken.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IThemeApplier _theme;
    private readonly ILoginItem _loginItem;

    /// <summary>
    /// True while the controls are being filled in from storage. Without it,
    /// loading raises the change events and writes back what was just read - the
    /// same trap the note window has with its geometry.
    /// </summary>
    private bool _loading;

    private Task _saving = Task.CompletedTask;

    public SettingsViewModel(SettingsService settings, IThemeApplier theme, ILoginItem loginItem)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(loginItem);

        _settings = settings;
        _theme = theme;
        _loginItem = loginItem;
    }

    [ObservableProperty]
    public partial NoteColor DefaultNoteColor { get; set; } = NoteColor.Yellow;

    [ObservableProperty]
    public partial AppTheme Theme { get; set; } = AppTheme.System;

    [ObservableProperty]
    public partial bool CheckForUpdates { get; set; } = true;

    [ObservableProperty]
    public partial bool StartAtLogin { get; set; } = true;

    /// <summary>
    /// Only an installed copy can start at login. Anywhere else the checkbox is
    /// shown disabled rather than hidden, so nobody wonders where it went.
    /// </summary>
    public bool CanChooseStartAtLogin => _loginItem.IsAvailable;

    public IReadOnlyList<NoteColor> AvailableColors { get; } = Enum.GetValues<NoteColor>();

    public IReadOnlyList<AppTheme> AvailableThemes { get; } = Enum.GetValues<AppTheme>();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _settings.LoadAsync(cancellationToken);

        _loading = true;
        try
        {
            DefaultNoteColor = stored.DefaultNoteColor;
            Theme = stored.Theme;
            CheckForUpdates = stored.CheckForUpdates;
            StartAtLogin = stored.StartAtLogin;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Waits for a save already started to finish. A setter cannot await, so
    /// without this there is no way to tell "not saved yet" from "saving now".
    /// </summary>
    public Task WhenSavedAsync() => _saving;

    partial void OnDefaultNoteColorChanged(NoteColor value) => Save();

    partial void OnCheckForUpdatesChanged(bool value) => Save();

    partial void OnStartAtLoginChanged(bool value)
    {
        if (_loading || !_loginItem.IsAvailable)
        {
            return;
        }

        try
        {
            _loginItem.Set(value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // The system said no, so the checkbox goes back to the truth and
            // nothing is saved. Going through _loading keeps putting it back from
            // counting as a change of its own.
            _loading = true;
            try
            {
                StartAtLogin = !value;
            }
            finally
            {
                _loading = false;
            }

            return;
        }

        Save();
    }

    partial void OnThemeChanged(AppTheme value)
    {
        if (_loading)
        {
            return;
        }

        // Applied as well as saved, and only on a real change: reapplying the
        // theme the app already started with would flicker for nothing.
        _theme.Apply(value);
        Save();
    }

    private void Save()
    {
        if (_loading)
        {
            return;
        }

        _saving = _settings.SaveAsync(new AppSettings
        {
            DefaultNoteColor = DefaultNoteColor,
            Theme = Theme,
            CheckForUpdates = CheckForUpdates,
            StartAtLogin = StartAtLogin,
        });
    }
}
