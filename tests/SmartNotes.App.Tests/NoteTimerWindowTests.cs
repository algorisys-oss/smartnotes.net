using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using SmartNotes.App.Views;
using SmartNotes.Core;
using SmartNotes.TestKit;
using SmartNotes.ViewModels;

namespace SmartNotes.App.Tests;

public class NoteTimerWindowTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public NoteTimerWindowTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
    }

    /// <summary>
    /// Opens the settings flyout and hands back its contents. A flyout builds its
    /// content when it is shown, so nothing inside it exists before this.
    /// </summary>
    private static StackPanel OpenSettings(NoteWindow window)
    {
        var button = window.FindControl<Button>("TimerSettingsButton")!;
        button.Flyout!.ShowAt(button);
        Dispatcher.UIThread.RunJobs();

        return ((Flyout)button.Flyout!).Content as StackPanel
            ?? throw new InvalidOperationException("the settings flyout has no content");
    }

    private static T InSettings<T>(NoteWindow window, string name)
        where T : Control
        => OpenSettings(window).GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    private NoteWindow OpenWindow(bool withTimer)
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        if (withTimer)
        {
            note.Timer = new NoteTimer { Duration = TimeSpan.FromMinutes(5), Label = "Back in" };
        }

        var viewModel = new NoteViewModel(
            note, _notes, _autoSave, new FakeWindowManager(), _clock, new InlineUiDispatcher());

        var window = new NoteWindow(viewModel);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void NoteWindow_ForANoteWithNoTimer_ShowsNoTimerBar()
    {
        var window = OpenWindow(withTimer: false);

        Assert.False(window.FindControl<Border>("TimerBar")!.IsVisible);
    }

    [AvaloniaFact]
    public void NoteWindow_ForANoteWithATimer_ShowsTheCountAndItsLabel()
    {
        var window = OpenWindow(withTimer: true);

        Assert.True(window.FindControl<Border>("TimerBar")!.IsVisible);
        Assert.Equal("5:00", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    [AvaloniaFact]
    public void NoteWindow_OnceTheTimerIsStarted_RedrawsTheCountAsTimePasses()
    {
        // The binding, not just the view-model: a number that only updates in a
        // unit test is not a timer anybody can use.
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;
        note.Timer!.StartCommand.Execute(null);

        _clock.Advance(TimeSpan.FromSeconds(61));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("3:59", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    [AvaloniaFact]
    public void TimerBar_WhileStopped_OffersStartAndNotPause()
    {
        var window = OpenWindow(withTimer: true);

        Assert.True(window.FindControl<Button>("TimerStart")!.IsVisible);
        Assert.False(window.FindControl<Button>("TimerPause")!.IsVisible);
    }

    [AvaloniaFact]
    public void TimerBar_WhileRunning_OffersPauseAndNotStart()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;

        note.Timer!.StartCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.FindControl<Button>("TimerStart")!.IsVisible);
        Assert.True(window.FindControl<Button>("TimerPause")!.IsVisible);
    }

    [AvaloniaFact]
    public void NoteWindow_WhenATimerIsAddedToAnOpenNote_ShowsTheBar()
    {
        var window = OpenWindow(withTimer: false);
        var note = (NoteViewModel)window.DataContext!;

        note.AddTimerCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.FindControl<Border>("TimerBar")!.IsVisible);
    }

    [AvaloniaFact]
    public void NoteWindow_WithATimerBar_IsStillMostlyGrabbableByItsTitleStrip()
    {
        // The bar is a second row, not a second thing in the title strip.
        var window = OpenWindow(withTimer: true);
        var header = window.FindControl<Grid>("Header")!;

        Assert.True(header.Bounds.Width > 50);
    }

    [AvaloniaFact]
    public void TimerBar_WhileStopped_LetsYouOpenTheSettings()
    {
        var window = OpenWindow(withTimer: true);

        Assert.True(window.FindControl<Button>("TimerSettingsButton")!.IsEnabled);
    }

    [AvaloniaFact]
    public void TimerBar_OnceRunning_LocksTheSettings()
    {
        // Moving the finish line halfway through is a way to be confused.
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;

        note.Timer!.StartCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.FindControl<Button>("TimerSettingsButton")!.IsEnabled);
    }

    [AvaloniaFact]
    public void TimerMinutes_WhenTyped_ChangesTheCount()
    {
        var window = OpenWindow(withTimer: true);

        InSettings<NumericUpDown>(window, "TimerMinutes").Value = 12;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("12:00", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    [AvaloniaFact]
    public void TimerLabel_WhenRenamed_ReachesTheNote()
    {
        var window = OpenWindow(withTimer: true);

        InSettings<TextBox>(window, "TimerLabelBox").Text = "Grabbing coffee";
        Dispatcher.UIThread.RunJobs();

        var note = (NoteViewModel)window.DataContext!;
        Assert.Equal("Grabbing coffee", note.Timer!.Label);
    }

    [AvaloniaFact]
    public void TimerDirectionToggle_SwitchesToCountingUp()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;

        InSettings<Button>(window, "TimerDirectionToggle").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(note.Timer!.IsCountingDown);
        Assert.Equal("0:00", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    [AvaloniaFact]
    public void TimerMinutes_WhenCountingUp_IsNotOffered()
    {
        // There is no length to set on something that counts up.
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;

        note.Timer!.ToggleDirectionCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // The whole "how long" section goes, not just the field: there is no
        // length to set on something that counts up.
        Assert.False(InSettings<StackPanel>(window, "TimerLengthSection").IsVisible);
    }

    /// <summary>
    /// The count stops at 0:00, so the colour is what says a break is over
    /// rather than not yet started.
    /// </summary>
    [AvaloniaFact]
    public void TimerDisplay_OnceTheCountdownRunsOut_ChangesColour()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;
        var display = window.FindControl<TextBlock>("TimerDisplay")!;
        var beforeFinishing = display.Foreground?.ToString();

        note.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("0:00", display.Text);
        Assert.True(note.Timer.HasFinished);
        Assert.NotEqual(beforeFinishing, display.Foreground?.ToString());
    }

    [AvaloniaFact]
    public void TimerBar_OffersAWayToStopAndRewind()
    {
        // Pause banks what has run and restart starts it going, so without this
        // there is no button that leaves a timer stopped at the top.
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;
        note.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(2));

        window.FindControl<Button>("TimerReset")!.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(note.Timer.IsRunning);
        Assert.Equal("5:00", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    [AvaloniaFact]
    public void TimerBar_AfterResetting_UnlocksTheSettingsAgain()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;
        note.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.FindControl<Button>("TimerSettingsButton")!.IsEnabled);

        window.FindControl<Button>("TimerReset")!.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.FindControl<Button>("TimerSettingsButton")!.IsEnabled);
    }

    /// <summary>
    /// Typing a length rather than using a preset. NumericUpDown commits as you
    /// type in Avalonia 12, which was checked rather than assumed.
    /// </summary>
    [AvaloniaFact]
    public void TimerMinutes_WhenTypedIntoDirectly_ReachesTheTimer()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;
        var inner = InSettings<NumericUpDown>(window, "TimerMinutes")
            .GetVisualDescendants().OfType<TextBox>().First();

        inner.Text = "12";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(12, note.Timer!.DurationMinutes);
        Assert.Equal("12:00", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    [AvaloniaFact]
    public void TimerSeconds_WhenTypedIntoDirectly_ReachesTheTimer()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;

        InSettings<NumericUpDown>(window, "TimerMinutes")
            .GetVisualDescendants().OfType<TextBox>().First().Text = "2";
        InSettings<NumericUpDown>(window, "TimerSeconds")
            .GetVisualDescendants().OfType<TextBox>().First().Text = "30";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, note.Timer!.DurationMinutes);
        Assert.Equal(30, note.Timer.DurationSeconds);
        Assert.Equal("2:30", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    /// <summary>
    /// Picking a preset gives that whole length, not that length minus whatever
    /// already ran - which is what it did, and read as the button being ignored.
    /// </summary>
    [AvaloniaFact]
    public void TimerPreset_PressedAfterSomeOfTheBreakRan_GivesTheWholeLength()
    {
        var window = OpenWindow(withTimer: true);
        var note = (NoteViewModel)window.DataContext!;
        note.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(3));
        note.Timer.PauseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        note.Timer.SetDurationCommand.Execute(10);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("10:00", window.FindControl<TextBlock>("TimerDisplay")!.Text);
    }

    /// <summary>
    /// The bar has to survive the narrowest a note can be. It did not before the
    /// settings moved into a flyout: a label, a direction toggle, four presets
    /// and a minutes-and-seconds pair were clipped to unreadable stumps.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(180)]
    [InlineData(280)]
    [InlineData(520)]
    public void TimerBar_AtAnyNoteWidth_KeepsTheCountAndItsButtonsWhole(int width)
    {
        var window = OpenWindow(withTimer: true);

        window.Width = width;
        Dispatcher.UIThread.RunJobs();

        var display = window.FindControl<TextBlock>("TimerDisplay")!;
        Assert.True(display.Bounds.Width > 0, $"the count has no width at {width}px");

        foreach (var name in new[] { "TimerStart", "TimerRestart", "TimerReset", "TimerSettingsButton" })
        {
            var button = window.FindControl<Button>(name)!;
            Assert.True(
                button.Bounds.Width > 0 && button.Bounds.Height > 0,
                $"{name} has collapsed at {width}px");
        }
    }

    [AvaloniaFact]
    public void TimerBar_HasNothingLeftToClip()
    {
        // Everything you set rather than press is in the flyout now, so the bar
        // itself holds only the count and its transport. FindControl would still
        // reach the spinners through the flyout's name scope, so this asks the
        // bar's own visual tree instead.
        var window = OpenWindow(withTimer: true);

        var onTheBar = window.FindControl<Border>("TimerBar")!
            .GetVisualDescendants()
            .OfType<NumericUpDown>()
            .ToList();

        Assert.Empty(onTheBar);
    }
}
