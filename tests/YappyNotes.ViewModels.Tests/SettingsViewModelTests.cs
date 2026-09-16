using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

public class SettingsViewModelTests
{
    private readonly InMemorySettingsRepository _repository = new();
    private readonly SettingsService _settings;
    private readonly FakeThemeApplier _theme = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public SettingsViewModelTests() => _settings = new SettingsService(_repository);

    private async Task<SettingsViewModel> LoadedAsync()
    {
        var viewModel = new SettingsViewModel(_settings, _theme);
        await viewModel.LoadAsync(Token);
        return viewModel;
    }

    private async Task<AppSettings> StoredAsync() => await _settings.LoadAsync(Token);

    [Fact]
    public async Task LoadAsync_OnAFirstRun_ShowsTheDefaults()
    {
        var viewModel = await LoadedAsync();

        Assert.Equal(NoteColor.Yellow, viewModel.DefaultNoteColor);
        Assert.Equal(AppTheme.System, viewModel.Theme);
    }

    [Fact]
    public async Task LoadAsync_ShowsWhatWasSavedBefore()
    {
        await _settings.SaveAsync(
            new AppSettings { DefaultNoteColor = NoteColor.Blue, Theme = AppTheme.Dark }, Token);

        var viewModel = await LoadedAsync();

        Assert.Equal(NoteColor.Blue, viewModel.DefaultNoteColor);
        Assert.Equal(AppTheme.Dark, viewModel.Theme);
    }

    /// <summary>
    /// Nothing in YappyNotes is saved by pressing a button, and settings are no
    /// exception.
    /// </summary>
    [Fact]
    public async Task DefaultNoteColor_WhenChanged_IsWrittenWithoutBeingAsked()
    {
        var viewModel = await LoadedAsync();

        viewModel.DefaultNoteColor = NoteColor.Pink;
        await viewModel.WhenSavedAsync();

        Assert.Equal(NoteColor.Pink, (await StoredAsync()).DefaultNoteColor);
    }

    [Fact]
    public async Task Theme_WhenChanged_IsWrittenAndAppliedStraightAway()
    {
        // Picking a theme and then having to restart to see it is the kind of
        // thing that makes a settings window feel broken.
        var viewModel = await LoadedAsync();

        viewModel.Theme = AppTheme.Dark;
        await viewModel.WhenSavedAsync();

        Assert.Equal(AppTheme.Dark, (await StoredAsync()).Theme);
        Assert.Equal(AppTheme.Dark, _theme.Applied[^1]);
    }

    [Fact]
    public async Task Theme_WhenChanged_KeepsTheOtherSettingAsItWas()
    {
        await _settings.SaveAsync(new AppSettings { DefaultNoteColor = NoteColor.Green }, Token);
        var viewModel = await LoadedAsync();

        viewModel.Theme = AppTheme.Light;
        await viewModel.WhenSavedAsync();

        Assert.Equal(NoteColor.Green, (await StoredAsync()).DefaultNoteColor);
    }

    /// <summary>
    /// The same trap the note window has with its geometry: filling the controls
    /// in raises their change events, which would write back what was just read.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FillingInTheControls_DoesNotWriteAnythingBack()
    {
        await _settings.SaveAsync(
            new AppSettings { DefaultNoteColor = NoteColor.Blue, Theme = AppTheme.Dark }, Token);
        var writesBefore = _repository.Writes;

        await LoadedAsync();

        Assert.Equal(writesBefore, _repository.Writes);
    }

    [Fact]
    public async Task LoadAsync_DoesNotReapplyTheThemeItJustRead()
    {
        // The app already applied the stored theme at startup; doing it again
        // from here would flicker for no reason.
        await _settings.SaveAsync(new AppSettings { Theme = AppTheme.Dark }, Token);

        await LoadedAsync();

        Assert.Empty(_theme.Applied);
    }

    [Fact]
    public async Task AvailableChoices_OfferEveryColourAndEveryTheme()
    {
        var viewModel = await LoadedAsync();

        Assert.Equal(Enum.GetValues<NoteColor>(), viewModel.AvailableColors);
        Assert.Equal(Enum.GetValues<AppTheme>(), viewModel.AvailableThemes);
    }
}
