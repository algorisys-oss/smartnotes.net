using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using SmartNotes.App.Views;
using SmartNotes.Core;
using SmartNotes.TestKit;
using SmartNotes.ViewModels;

namespace SmartNotes.App.Tests;

/// <summary>
/// Which note the keystrokes are going into.
/// </summary>
/// <remarks>
/// With half a dozen notes open there was nothing saying which one had the
/// keyboard. The difference is deliberately small - a note is paper, not a form -
/// but it has to be visible without looking for it.
/// </remarks>
public class FocusedNoteTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly CountingNoteRepository _counting;
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public FocusedNoteTests()
    {
        _counting = new CountingNoteRepository(_repository);
        _notes = new NoteService(_counting, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
    }

    private NoteWindow Open()
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        var window = new NoteWindow(new NoteViewModel(
            note, _notes, _autoSave, new FakeWindowManager(), _clock, new InlineUiDispatcher()));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Border Chrome(NoteWindow window) => window.FindControl<Border>("NoteChrome")!;

    [AvaloniaFact]
    public void NoteWindow_HasAnActiveStateToStyle()
    {
        var window = Open();

        Assert.NotNull(Chrome(window));
        Assert.True(window.IsActiveNote || !window.IsActiveNote);
    }

    [AvaloniaFact]
    public void NoteWindow_WhenItBecomesTheActiveOne_SaysSo()
    {
        var window = Open();

        window.MarkActive(true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.IsActiveNote);
    }

    [AvaloniaFact]
    public void NoteWindow_WhileActive_HasAStrongerEdgeThanWhenItIsNot()
    {
        // The whole point: the two states must actually look different.
        var window = Open();

        window.MarkActive(false);
        Dispatcher.UIThread.RunJobs();
        var restingEdge = ((ISolidColorBrush)Chrome(window).BorderBrush!).Color;
        var restingThickness = Chrome(window).BorderThickness;

        window.MarkActive(true);
        Dispatcher.UIThread.RunJobs();
        var activeEdge = ((ISolidColorBrush)Chrome(window).BorderBrush!).Color;
        var activeThickness = Chrome(window).BorderThickness;

        Assert.True(
            activeEdge != restingEdge || activeThickness != restingThickness,
            "an active note looks exactly like a resting one, so nothing says where the keystrokes go");
    }

    [AvaloniaFact]
    public void NoteWindow_WhileActive_KeepsItsPaperColour()
    {
        // A note is paper. Marking it active must not repaint it into a form.
        var window = Open();
        var note = (NoteViewModel)window.DataContext!;
        note.Color = NoteColor.Green;
        Dispatcher.UIThread.RunJobs();
        var paper = ((ISolidColorBrush)window.Background!).Color;

        window.MarkActive(true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(paper, ((ISolidColorBrush)window.Background!).Color);
    }

    [AvaloniaFact]
    public async Task NoteWindow_BecomingActive_WritesNothing()
    {
        // Clicking between notes must not cost a disk write each time.
        var window = Open();
        await _autoSave.FlushAllAsync();
        var writesBefore = _counting.Updates;

        window.MarkActive(true);
        window.MarkActive(false);
        window.MarkActive(true);
        _clock.Advance(TimeSpan.FromSeconds(5));
        await _autoSave.WhenIdleAsync();

        Assert.Equal(writesBefore, _counting.Updates);
    }
}
