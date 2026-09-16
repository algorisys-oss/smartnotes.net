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

    /// <summary>
    /// True while the controls are being filled in from storage. Without it,
    /// loading raises the change events and writes back what was just read - the
    /// same trap the note window has with its geometry.
    /// </summary>
    private bool _loading;

    private Task _saving = Task.CompletedTask;

    public SettingsViewModel(SettingsService settings, IThemeApplier theme)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(theme);

        _settings = settings;
        _theme = theme;
    }

    [ObservableProperty]
    public partial NoteColor DefaultNoteColor { get; set; } = NoteColor.Yellow;

    [ObservableProperty]
    public partial AppTheme Theme { get; set; } = AppTheme.System;

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
        });
    }
}
