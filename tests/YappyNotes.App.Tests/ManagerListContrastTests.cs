using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

/// <summary>
/// A note row is a scrap of light paper whichever theme the manager is in, so
/// everything drawn on it has to resolve light too.
/// </summary>
/// <remarks>
/// This is a real bug that shipped: in dark mode the Open and Archive buttons
/// took the application's variant and rendered pale text on a pastel background,
/// leaving them all but invisible.
/// </remarks>
public class ManagerListContrastTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;

    public ManagerListContrastTests() => _notes = new NoteService(_repository, _clock);

    private ManagerWindow OpenWithANote(ThemeVariant appTheme)
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        note.Title = "Shopping";
        _notes.SaveAsync(note).GetAwaiter().GetResult();

        Application.Current!.RequestedThemeVariant = appTheme;

        var manager = new ManagerViewModel(_notes, new FakeWindowManager());
        var window = new ManagerWindow();
        window.Bind(manager);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static ThemeVariant RowVariant(ManagerWindow window)
    {
        var scope = window.GetVisualDescendants()
            .OfType<ThemeVariantScope>()
            .FirstOrDefault();

        Assert.True(scope is not null, "the note rows are not inside a ThemeVariantScope at all");
        return scope!.ActualThemeVariant;
    }

    [AvaloniaFact]
    public void NoteRows_WithTheManagerInDarkMode_StillResolveLight()
    {
        var window = OpenWithANote(ThemeVariant.Dark);

        Assert.Equal(ThemeVariant.Light, RowVariant(window));
    }

    [AvaloniaFact]
    public void NoteRows_WithTheManagerInLightMode_ResolveLightToo()
    {
        var window = OpenWithANote(ThemeVariant.Light);

        Assert.Equal(ThemeVariant.Light, RowVariant(window));
    }

    [AvaloniaFact]
    public void NoteWindow_WhateverTheAppTheme_IsAlwaysLightPaper()
    {
        // The same rule, on the window that already followed it.
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        var autoSave = new AutoSaveService(_notes, _clock);
        var window = new NoteWindow(new NoteViewModel(note, _notes, autoSave, new FakeWindowManager()));

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
    }
}
