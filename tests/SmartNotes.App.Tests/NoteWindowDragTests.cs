using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using SmartNotes.App.Views;
using SmartNotes.Core;
using SmartNotes.TestKit;
using SmartNotes.ViewModels;

namespace SmartNotes.App.Tests;

/// <summary>
/// Whether a note can be picked up and resized at all.
/// </summary>
/// <remarks>
/// The native drag itself cannot be tested headlessly - BeginMoveDrag hands over
/// to the window manager. What can be tested, and what actually broke, is
/// whether there is anywhere left to press: the title TextBox filled the whole
/// strip and swallowed every press, so the window had no grab area and no test
/// noticed.
/// </remarks>
public class NoteWindowDragTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public NoteWindowDragTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
    }

    private NoteWindow OpenWindow()
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        note.Title = "Break";
        var window = new NoteWindow(new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager()));
        window.Show();

        // Without a layout pass every Bounds is still empty, and a test that
        // samples points inside a zero-width strip measures nothing while
        // reporting success - which is how these two first passed against the
        // very bug they exist to catch.
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>
    /// The points along the title strip that are not covered by a control, out of
    /// twenty sampled.
    /// </summary>
    private static int GrabbablePointsAcross(NoteWindow window)
    {
        var header = window.FindControl<Grid>("Header")!;

        Assert.True(
            header.Bounds.Width > 50 && header.Bounds.Height > 0,
            $"the title strip has not been laid out ({header.Bounds}), so this test would "
            + "sample nothing and pass regardless of what is in the way");

        var middle = header.Bounds.Height / 2;

        return Enumerable.Range(1, 20)
            .Select(step => header.Bounds.Width * step / 21)
            .Count(x => !IsSwallowedByAControl(window, new Point(x, middle)));
    }

    /// <summary>Whatever is under this point, is it inside an interactive control?</summary>
    private static bool IsSwallowedByAControl(NoteWindow window, Point point)
    {
        var hit = ((IInputElement)window).InputHitTest(point) as Visual;

        return hit?.GetSelfAndVisualAncestors()
            .TakeWhile(v => v is not Window)
            .Any(v => v is TextBox or Button) ?? false;
    }

    [AvaloniaFact]
    public void NoteWindow_AcrossItsTitleStrip_HasSomewhereToPickItUpBy()
    {
        var window = OpenWindow();

        var grabbable = GrabbablePointsAcross(window);

        Assert.True(
            grabbable > 0,
            "Nowhere on the title strip is free of a TextBox or Button, so the note "
            + "cannot be dragged anywhere along it.");
    }

    /// <summary>
    /// Most of the strip, not a sliver of it. A note you can only pick up by a
    /// three-pixel gap is a note nobody can move.
    /// </summary>
    [AvaloniaFact]
    public void NoteWindow_MostOfItsTitleStrip_IsGrabbable()
    {
        var window = OpenWindow();

        var grabbable = GrabbablePointsAcross(window);

        Assert.True(grabbable >= 10, $"only {grabbable} of 20 points along the strip are grabbable");
    }

    [AvaloniaFact]
    public void NoteWindow_HasAResizeGripInItsBottomCorner()
    {
        // Nothing else can resize it: the OS draws no frame for a window with no
        // decorations, so it supplies no handles either.
        var window = OpenWindow();

        var grip = window.FindControl<Panel>("ResizeGrip");

        Assert.NotNull(grip);
        Assert.True(grip.Bounds.Width > 0 && grip.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void TitleBox_BeforeAnyoneDoubleClicksIt_StaysOutOfThePointersWay()
    {
        var window = OpenWindow();

        var titleBox = window.FindControl<TextBox>("TitleBox")!;

        Assert.False(titleBox.IsHitTestVisible);
    }
}
