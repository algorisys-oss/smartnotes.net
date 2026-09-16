using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

public class SettingsWindowTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _notesRepository = new();
    private readonly InMemorySettingsRepository _settingsRepository = new();
    private readonly SettingsService _settings;
    private readonly WindowManager _windows;

    public SettingsWindowTests()
    {
        _settings = new SettingsService(_settingsRepository);
        var notes = new NoteService(_notesRepository, _clock, _settings);
        var autoSave = new AutoSaveService(notes, _clock, TimeSpan.FromMilliseconds(750));

        _windows = new WindowManager(
            notes, autoSave, () => new SettingsViewModel(_settings, new ThemeApplier()));
    }

    private SettingsViewModel OpenSettings()
    {
        _windows.ShowSettings();
        Dispatcher.UIThread.RunJobs();
        return _windows.OpenSettings!;
    }

    [AvaloniaFact]
    public async Task ShowSettings_OpensAWindowShowingWhatIsStored()
    {
        await _settings.SaveAsync(
            new AppSettings { DefaultNoteColor = NoteColor.Blue, Theme = AppTheme.Dark });

        var viewModel = OpenSettings();
        await viewModel.WhenSavedAsync();

        Assert.Equal(NoteColor.Blue, viewModel.DefaultNoteColor);
        Assert.Equal(AppTheme.Dark, viewModel.Theme);
    }

    [AvaloniaFact]
    public void ShowSettings_Twice_BringsTheSameWindowForwardRatherThanOpeningTwo()
    {
        var first = OpenSettings();

        var second = OpenSettings();

        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void ShowSettings_WithNoSettingsFactory_OpensNothingRatherThanThrowing()
    {
        // Every note-window test builds a WindowManager without one.
        var notes = new NoteService(_notesRepository, _clock);
        var bare = new WindowManager(notes, new AutoSaveService(notes, _clock));

        bare.ShowSettings();

        Assert.Null(bare.OpenSettings);
    }

    [AvaloniaFact]
    public async Task SettingsWindow_ChangingTheDefaultColour_WritesItWithoutAButton()
    {
        var viewModel = OpenSettings();

        viewModel.DefaultNoteColor = NoteColor.Purple;
        await viewModel.WhenSavedAsync();

        Assert.Equal(NoteColor.Purple, (await _settings.LoadAsync()).DefaultNoteColor);
    }

    [AvaloniaFact]
    public async Task SettingsWindow_ChangingTheDefaultColour_ChangesWhatANewNoteLooksLike()
    {
        // The whole point of the setting, end to end.
        var viewModel = OpenSettings();
        viewModel.DefaultNoteColor = NoteColor.Grey;
        await viewModel.WhenSavedAsync();

        var notes = new NoteService(_notesRepository, _clock, _settings);
        var note = await notes.CreateAsync();

        Assert.Equal(NoteColor.Grey, note.Color);
    }
}
