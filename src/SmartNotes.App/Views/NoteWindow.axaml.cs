using Avalonia.Controls;
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

        // Window exposes no styled property for its position, so this is the
        // only way to hear about a drag finishing somewhere new.
        PositionChanged += OnPositionChanged;
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
            Topmost = note.IsAlwaysOnTop;
        }
        finally
        {
            _applyingGeometry = false;
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

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        // Dragging the strip moves the window; the title box inside it still gets
        // its own clicks, so only a press on the strip itself starts a drag.
        if (e.Source is TextBox or Button)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
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
