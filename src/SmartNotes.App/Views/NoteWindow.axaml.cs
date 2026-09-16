using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SmartNotes.ViewModels;

namespace SmartNotes.App.Views;

/// <summary>
/// One sticky note on the desktop: a borderless window that remembers where it
/// was put.
/// </summary>
public partial class NoteWindow : Window
{
    private NoteViewModel? _note;

    /// <summary>
    /// True while the window is applying the view-model's geometry to itself.
    /// Without it, restoring a note's saved position raises PositionChanged,
    /// which writes the position back, which schedules a save - so opening a note
    /// you never touched would rewrite it.
    /// </summary>
    private bool _applyingGeometry;

    private bool _closeConfirmed;

    public NoteWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var header = this.FindControl<Grid>("Header");
        if (header is not null)
        {
            header.PointerPressed += OnHeaderPressed;
        }

        var grip = this.FindControl<Panel>("ResizeGrip");
        if (grip is not null)
        {
            grip.PointerPressed += OnResizeGripPressed;
        }

        var titleBox = this.FindControl<TextBox>("TitleBox");
        if (titleBox is not null)
        {
            // Editing ends when the title loses focus, and the strip goes back
            // to being something you can pick the window up by.
            titleBox.LostFocus += (_, _) => titleBox.IsHitTestVisible = false;
        }

        // Window exposes no styled property for its position, so this is the
        // only way to hear about a drag finishing somewhere new.
        PositionChanged += OnPositionChanged;

        // Which note the keystrokes are going into. With several open there was
        // nothing saying so.
        Activated += (_, _) => MarkActive(true);
        Deactivated += (_, _) => MarkActive(false);
        MarkActive(false);
    }

    public NoteWindow(NoteViewModel note) : this() => Bind(note);

    public void Bind(NoteViewModel note)
    {
        ArgumentNullException.ThrowIfNull(note);

        _note = note;
        DataContext = note;

        _applyingGeometry = true;
        try
        {
            Width = note.Width;
            Height = note.Height;
            Position = new Avalonia.PixelPoint(note.X, note.Y);
            // Topmost is bound in XAML, so pinning follows the view-model rather
            // than being frozen at whatever it was when the window opened.
        }
        finally
        {
            _applyingGeometry = false;
        }
    }

    /// <summary>
    /// Ctrl+W and Escape close the window and keep the note. Handled here rather
    /// than as a KeyBinding because closing is the window's business, not the
    /// view-model's - the view-model has no idea a window exists.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape || (e.Key == Key.W && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            e.Handled = true;
            Close();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // A new note should be ready to type into without a click.
        this.FindControl<TextBox>("Body")?.Focus();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_closeConfirmed || _note is null)
        {
            return;
        }

        // Closing is synchronous and flushing is not, so the close is called off
        // and repeated once whatever the debounce was holding has been written.
        // Letting it through first would lose the last sentence typed.
        e.Cancel = true;
        _ = FlushThenCloseAsync(_note);
    }

    private async Task FlushThenCloseAsync(NoteViewModel note)
    {
        await note.CloseAsync();

        _closeConfirmed = true;
        Close();
    }

    /// <summary>Whether this is the note with the keyboard.</summary>
    public bool IsActiveNote { get; private set; }

    /// <summary>
    /// Marks this note as the active one, or not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the edge changes. The paper keeps its colour - a note is paper, not a
    /// form, and repainting it on focus would turn the desktop into a flicker.
    /// A darker, slightly thicker border reads at a glance without asking to be
    /// looked at.
    /// </para>
    /// <para>
    /// Purely visual: nothing here touches the note, so clicking between windows
    /// costs no disk write. <c>FocusedNoteTests</c> holds both of those.
    /// </para>
    /// </remarks>
    public void MarkActive(bool active)
    {
        IsActiveNote = active;

        var chrome = this.FindControl<Border>("NoteChrome");
        if (chrome is null)
        {
            return;
        }

        chrome.BorderBrush = active ? ActiveEdge : RestingEdge;
        chrome.BorderThickness = new Thickness(active ? 2 : 1);
    }

    private static readonly IBrush ActiveEdge = new SolidColorBrush(Color.FromArgb(0xBB, 0x00, 0x00, 0x00));
    private static readonly IBrush RestingEdge = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00));

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    /// <summary>
    /// The strip is how you pick a note up, and - on a double-click - how you
    /// rename it.
    /// </summary>
    /// <remarks>
    /// The buttons in the strip mark their own presses handled, and this is
    /// attached for unhandled events only, so pressing one never starts a drag.
    /// Checking <c>e.Source</c> for a Button or a TextBox would not have worked:
    /// the source is the template part under the pointer - a ContentPresenter, a
    /// TextPresenter - and never the control itself.
    /// </remarks>
    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            BeginEditingTitle();
            return;
        }

        BeginMoveDrag(e);
    }

    private void BeginEditingTitle()
    {
        var titleBox = this.FindControl<TextBox>("TitleBox");
        if (titleBox is null)
        {
            return;
        }

        titleBox.IsHitTestVisible = true;
        titleBox.Focus();
        titleBox.SelectAll();
    }

    /// <summary>
    /// The corner grip. A window with no decorations gets no resize handles from
    /// the OS, so without this a note is stuck at whatever size it was created.
    /// </summary>
    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.SouthEast, e);
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_note is null || _applyingGeometry)
        {
            return;
        }

        _note.X = e.Point.X;
        _note.Y = e.Point.Y;
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_note is null || _applyingGeometry)
        {
            return;
        }

        if (change.Property == WidthProperty || change.Property == HeightProperty)
        {
            _note.Width = (int)Width;
            _note.Height = (int)Height;
        }
    }
}
