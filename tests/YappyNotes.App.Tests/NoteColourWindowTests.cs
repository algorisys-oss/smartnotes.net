using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

/// <summary>
/// Recolouring a note from a button you can see.
/// </summary>
/// <remarks>
/// The colours used to be reachable only from the note's context menu, and
/// right-clicking the body - most of the note - opens the text box's own
/// Cut/Copy/Paste menu instead. Reported from real use as "not very obvious".
/// </remarks>
public class NoteColourWindowTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly CountingNoteRepository _counting = new(new InMemoryNoteRepository());
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public NoteColourWindowTests()
    {
        _notes = new NoteService(_counting, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, Debounce);
    }

    private NoteWindow OpenWindow(NoteColor color)
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        note.Color = color;

        var window = new NoteWindow(new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager()));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// Opens the colour flyout and hands back its swatches. A flyout builds its
    /// content when it is shown, so nothing inside it exists before this.
    /// </summary>
    private static ListBox OpenSwatches(NoteWindow window)
    {
        var button = window.FindControl<Button>("ColorButton")
            ?? throw new InvalidOperationException("the note has no ColorButton");
        var flyout = button.Flyout
            ?? throw new InvalidOperationException("the ColorButton has no flyout");
        flyout.ShowAt(button);
        Dispatcher.UIThread.RunJobs();

        var content = ((Flyout)flyout).Content as Control
            ?? throw new InvalidOperationException("the colour flyout has no content");

        return content.GetSelfAndLogicalDescendants().OfType<ListBox>().Single(c => c.Name == "ColorSwatches");
    }

    [AvaloniaFact]
    public void TitleStrip_OnAnyNote_HasAColourButtonWithAFlyout()
    {
        var window = OpenWindow(NoteColor.Yellow);

        var button = window.FindControl<Button>("ColorButton");

        Assert.NotNull(button);
        Assert.True(button.IsVisible);
        Assert.NotNull(button.Flyout);
        Assert.Contains(button, window.FindControl<Grid>("Header")!.GetVisualDescendants());
    }

    [AvaloniaFact]
    public void ColourFlyout_WhenOpened_OffersEveryColour()
    {
        var window = OpenWindow(NoteColor.Yellow);

        var swatches = OpenSwatches(window);

        Assert.Equal(Enum.GetValues<NoteColor>(), swatches.Items.Cast<NoteColor>());
    }

    [AvaloniaFact]
    public void ColourFlyout_WhenOpened_MarksTheColourTheNoteIs()
    {
        var window = OpenWindow(NoteColor.Pink);

        var swatches = OpenSwatches(window);

        Assert.Equal(NoteColor.Pink, swatches.SelectedItem);
    }

    [AvaloniaFact]
    public void ColourFlyout_PickingAColour_RepaintsTheNote()
    {
        var window = OpenWindow(NoteColor.Yellow);
        var swatches = OpenSwatches(window);

        swatches.SelectedItem = NoteColor.Blue;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(NoteColor.Blue, ((NoteViewModel)window.DataContext!).Color);
        Assert.Equal(
            ((SolidColorBrush)NoteBrushes.Paper.Convert(NoteColor.Blue, typeof(IBrush), null, null!)).Color,
            ((SolidColorBrush)window.Background!).Color);
    }

    /// <summary>
    /// The swatches are bound two-way to the note's colour, so the binding writing
    /// its own value back as the flyout opens is exactly the kind of thing that
    /// would rewrite a note nobody changed.
    /// </summary>
    [AvaloniaFact]
    public async Task ColourFlyout_OpenedAndClosedWithoutAPick_WritesNothing()
    {
        var window = OpenWindow(NoteColor.Green);
        var writesBefore = _counting.Updates;

        OpenSwatches(window);
        window.FindControl<Button>("ColorButton")!.Flyout!.Hide();
        Dispatcher.UIThread.RunJobs();
        await _autoSave.FlushAllAsync();

        Assert.Equal(writesBefore, _counting.Updates);
    }
}
