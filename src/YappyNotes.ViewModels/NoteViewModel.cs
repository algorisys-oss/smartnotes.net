using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>
/// One sticky note, as the window bound to it sees it.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Note"/> is the single copy of the state: every property here
/// reads and writes straight through to it rather than keeping a second copy in
/// sync. That is why <c>[ObservableProperty]</c> is not used - the generated
/// backing field would be exactly the duplicate this is avoiding, and the two
/// would disagree the first time anything changed the note from elsewhere.
/// </para>
/// <para>
/// Every setter that actually changes something schedules an autosave. Setting a
/// property to the value it already holds writes nothing, so a window
/// re-applying its bindings costs no disk.
/// </para>
/// </remarks>
public sealed partial class NoteViewModel : ObservableObject
{
    private readonly Note _note;
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;
    private readonly IWindowManager _windows;
    private readonly TimeProvider _timeProvider;
    private readonly IUiDispatcher _ui;
    private readonly ILinkLauncher _links;

    /// <param name="timeProvider">
    /// Used by the timer, if the note has one. Defaults to the system clock so
    /// that the many tests which do not care about timers need not supply it.
    /// </param>
    /// <param name="ui">
    /// How a tick gets back to the UI thread. Defaults to running the work where
    /// it was posted from, which is what a test wants and what a note with no
    /// timer never exercises.
    /// </param>
    public NoteViewModel(
        Note note,
        NoteService notes,
        AutoSaveService autoSave,
        IWindowManager windows,
        TimeProvider? timeProvider = null,
        IUiDispatcher? ui = null,
        ILinkLauncher? links = null)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(autoSave);
        ArgumentNullException.ThrowIfNull(windows);

        _note = note;
        _notes = notes;
        _autoSave = autoSave;
        _windows = windows;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ui = ui ?? new ImmediateUiDispatcher();
        _links = links ?? new NoLinkLauncher();

        AttachTimer();
        ScanForLinks();
    }

    public Guid Id => _note.Id;

    public string Title
    {
        get => _note.Title;
        set => Set(_note.Title, value, v => _note.Title = v);
    }

    public string Content
    {
        get => _note.Content;
        set
        {
            Set(_note.Content, value, v => _note.Content = v);
            ScanForLinks();
        }
    }

    /// <summary>
    /// The links in this note's text, as they are typed.
    /// </summary>
    /// <remarks>
    /// Offered beside the note rather than made clickable inside it: the body is
    /// an editable TextBox, which draws plain text and nothing else, and turning
    /// it into something that renders runs of formatting would be a far larger
    /// change than links are worth. Only allow-listed schemes get this far.
    /// </remarks>
    public IReadOnlyList<LinkSpan> Links { get; private set; } = [];

    public bool HasLinks => Links.Count > 0;

    [RelayCommand]
    public async Task OpenLinkAsync(LinkSpan? link)
    {
        if (link is not null)
        {
            await _links.OpenAsync(link.Uri);
        }
    }

    private void ScanForLinks()
    {
        var found = LinkScanner.Scan(_note.Content);

        if (found.Count == 0 && Links.Count == 0)
        {
            return;
        }

        Links = found;
        OnPropertyChanged(nameof(Links));
        OnPropertyChanged(nameof(HasLinks));
    }

    public NoteColor Color
    {
        get => _note.Color;
        set => Set(_note.Color, value, v => _note.Color = v);
    }

    public bool IsAlwaysOnTop
    {
        get => _note.IsAlwaysOnTop;
        set => Set(_note.IsAlwaysOnTop, value, v => _note.IsAlwaysOnTop = v);
    }

    public int X
    {
        get => _note.X;
        set => Set(_note.X, value, v => _note.X = v);
    }

    public int Y
    {
        get => _note.Y;
        set => Set(_note.Y, value, v => _note.Y = v);
    }

    public int Width
    {
        get => _note.Width;
        set => Set(_note.Width, value, v => _note.Width = v);
    }

    public int Height
    {
        get => _note.Height;
        set => Set(_note.Height, value, v => _note.Height = v);
    }

    /// <summary>The note's counter, if it has one.</summary>
    public NoteTimerViewModel? Timer { get; private set; }

    public bool HasTimer => Timer is not null;

    /// <summary>Gives this note a five-minute countdown, ready to start.</summary>
    [RelayCommand]
    public void AddTimer()
    {
        if (_note.Timer is not null)
        {
            return;
        }

        _note.Timer = new NoteTimer
        {
            Direction = TimerDirection.CountDown,
            Duration = TimeSpan.FromMinutes(5),
            Label = "Back in",
        };

        AttachTimer();
        AnnounceTimer();
        _autoSave.Schedule(_note);
    }

    [RelayCommand]
    public void RemoveTimer()
    {
        if (_note.Timer is null)
        {
            return;
        }

        Timer?.Dispose();
        Timer = null;
        _note.Timer = null;

        AnnounceTimer();
        _autoSave.Schedule(_note);
    }

    private void AttachTimer()
        => Timer = _note.Timer is null
            ? null
            : new NoteTimerViewModel(_note.Timer, _timeProvider, _ui, () => _autoSave.Schedule(_note));

    private void AnnounceTimer()
    {
        OnPropertyChanged(nameof(Timer));
        OnPropertyChanged(nameof(HasTimer));
    }

    private static readonly IReadOnlyList<NoteColor> Palette = Enum.GetValues<NoteColor>();

    /// <summary>
    /// The colours this note could be painted. An instance property because that
    /// is what a XAML ItemsSource can bind to; the list itself is shared.
    /// </summary>
    public IReadOnlyList<NoteColor> AvailableColors => Palette;

    /// <summary>Pins the note above other windows, or lets it back down.</summary>
    [RelayCommand]
    public void TogglePin() => IsAlwaysOnTop = !IsAlwaysOnTop;

    /// <summary>
    /// Repaints the note. Going through the property rather than the note means
    /// an unchanged colour still costs nothing.
    /// </summary>
    [RelayCommand]
    public void SetColor(NoteColor color) => Color = color;

    /// <summary>
    /// What the delete button does: the note is filed away, not destroyed, and
    /// its window goes. Anything typed a moment ago is written first.
    /// </summary>
    [RelayCommand]
    public async Task ArchiveAsync()
    {
        await _autoSave.FlushAsync(Id);
        await _notes.ArchiveAsync(Id);
        _windows.CloseNote(Id);
    }

    /// <summary>
    /// The window is closing. Writes whatever the debounce was still holding -
    /// cancelling it would lose the last sentence someone typed.
    /// </summary>
    public Task CloseAsync() => _autoSave.FlushAsync(Id);

    private void Set<T>(T current, T value, Action<T> assign, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        assign(value);
        OnPropertyChanged(propertyName);
        _autoSave.Schedule(_note);
    }
}
