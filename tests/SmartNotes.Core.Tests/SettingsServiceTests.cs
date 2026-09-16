using SmartNotes.Core;
using SmartNotes.TestKit;

namespace SmartNotes.Core.Tests;

public class SettingsServiceTests
{
    private readonly InMemorySettingsRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SettingsService NewService() => new(_repository);

    [Fact]
    public async Task LoadAsync_OnAFirstRun_HandsBackSensibleDefaults()
    {
        var settings = await NewService().LoadAsync(Token);

        Assert.Equal(NoteColor.Yellow, settings.DefaultNoteColor);
        Assert.Equal(AppTheme.System, settings.Theme);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_GivesBackWhatWasSaved()
    {
        var service = NewService();

        await service.SaveAsync(new AppSettings
        {
            DefaultNoteColor = NoteColor.Green,
            Theme = AppTheme.Dark,
        }, Token);

        var reloaded = await service.LoadAsync(Token);
        Assert.Equal(NoteColor.Green, reloaded.DefaultNoteColor);
        Assert.Equal(AppTheme.Dark, reloaded.Theme);
    }

    /// <summary>
    /// A settings file is the easiest thing in the world to hand-edit into
    /// nonsense, and it is read on every start. Falling over at that point would
    /// mean an app that cannot be opened to fix it.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WithAValueNothingUnderstands_FallsBackInsteadOfThrowing()
    {
        await _repository.SetAsync("theme", "Chartreuse", Token);
        await _repository.SetAsync("defaultNoteColor", "Octarine", Token);

        var settings = await NewService().LoadAsync(Token);

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(NoteColor.Yellow, settings.DefaultNoteColor);
    }

    [Fact]
    public async Task LoadAsync_WithOnlyOneSettingStored_LeavesTheOtherAtItsDefault()
    {
        await _repository.SetAsync("theme", "Light", Token);

        var settings = await NewService().LoadAsync(Token);

        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.Equal(NoteColor.Yellow, settings.DefaultNoteColor);
    }
}
