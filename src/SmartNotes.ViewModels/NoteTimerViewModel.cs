using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartNotes.Core;

namespace SmartNotes.ViewModels;

/// <summary>
/// A note's counter, as the window shows it.
/// </summary>
/// <remarks>
/// <para>
/// Ticking repaints and writes nothing. The stored timer does not change between
/// one second and the next - only starting, pausing and restarting change it -
/// so the autosave is left alone and needs no special case to avoid a note that
/// would otherwise be dirty forever.
/// </para>
/// <para>
/// The ticker only runs while the timer does. Waking up once a second to redraw
/// a number that cannot have changed is work nobody asked for.
/// </para>
/// </remarks>
public sealed partial class NoteTimerViewModel : ObservableObject, IDisposable
{
    private readonly NoteTimer _timer;
    private readonly TimeProvider _timeProvider;
    private readonly IUiDispatcher _ui;
    private readonly Action _onTransition;

    private ITimer? _ticker;
    private bool _disposed;

    public NoteTimerViewModel(
        NoteTimer timer,
        TimeProvider timeProvider,
        IUiDispatcher ui,
        Action onTransition)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(onTransition);

        _timer = timer;
        _timeProvider = timeProvider;
        _ui = ui;
        _onTransition = onTransition;

        if (_timer.IsRunning)
        {
            StartTicking();
        }
    }

    /// <summary>The shortest countdown worth having.</summary>
    private const int ShortestMinutes = 1;

    /// <summary>What the counter is for. Renaming it is a change worth keeping.</summary>
    public string Label
    {
        get => _timer.Label;
        set => Change(value, _timer.Label, v => _timer.Label = v);
    }

    /// <summary>The whole minutes part of a countdown's length.</summary>
    public int DurationMinutes
    {
        get => (int)_timer.Duration.TotalMinutes;
        set => SetLength(Math.Max(0, value), DurationSeconds);
    }

    /// <summary>The seconds part, for a break that is not a whole number of minutes.</summary>
    public int DurationSeconds
    {
        get => _timer.Duration.Seconds;
        set => SetLength(DurationMinutes, Math.Clamp(value, 0, 59));
    }

    /// <summary>
    /// Sets how long the countdown runs for, and starts it over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Banked time is cleared. Choosing a length means "a break this long", not
    /// "this long minus what I already used" - keeping the accumulated time made
    /// picking 10 minutes show 8:00, which reads as the box having ignored you.
    /// </para>
    /// <para>
    /// Zero is not a length: a countdown of nothing is finished before it starts.
    /// Under a second, it falls back to the shortest one worth having.
    /// </para>
    /// </remarks>
    private void SetLength(int minutes, int seconds)
    {
        var wanted = new TimeSpan(0, minutes, seconds);
        if (wanted <= TimeSpan.Zero)
        {
            wanted = TimeSpan.FromMinutes(ShortestMinutes);
        }

        if (wanted == _timer.Duration && _timer.Accumulated == TimeSpan.Zero)
        {
            return;
        }

        _timer.Duration = wanted;
        _timer.Accumulated = TimeSpan.Zero;

        Redraw();
        _onTransition();
    }

    public bool IsCountingDown => _timer.Direction == TimerDirection.CountDown;

    /// <summary>
    /// Whether the length and direction may be changed. Not while it is running:
    /// moving the finish line halfway through is a way to be confused rather than
    /// a feature.
    /// </summary>
    public bool CanEdit => !_timer.IsRunning;

    /// <summary>Durations a stream break actually tends to be.</summary>
    public IReadOnlyList<int> DurationPresets { get; } = [5, 10, 15, 30];

    public string Display => _timer.DisplayAt(_timeProvider.GetUtcNow());

    public bool IsRunning => _timer.IsRunning;

    public bool HasFinished => _timer.HasFinishedAt(_timeProvider.GetUtcNow());

    /// <summary>Counts up instead of down, or back again.</summary>
    [RelayCommand]
    public void ToggleDirection() => Transition(_ => _timer.Direction =
        _timer.Direction == TimerDirection.CountDown ? TimerDirection.CountUp : TimerDirection.CountDown);

    [RelayCommand]
    public void SetDuration(int minutes) => SetLength(minutes, 0);

    /// <summary>
    /// Changes a stored field, and only asks for a write when something actually
    /// changed - the same rule every other setter in the app follows.
    /// </summary>
    private void Change<T>(T value, T current, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        assign(value);
        Redraw();
        _onTransition();
    }

    [RelayCommand]
    public void Start() => Transition(now => _timer.Start(now));

    [RelayCommand]
    public void Pause() => Transition(now => _timer.Pause(now));

    [RelayCommand]
    public void Restart() => Transition(now => _timer.Restart(now));

    [RelayCommand]
    public void Reset() => Transition(_ => _timer.Reset());

    public void Dispose()
    {
        _disposed = true;
        StopTicking();
    }

    /// <summary>
    /// The only things that change what is stored, and so the only things that
    /// ask for a write.
    /// </summary>
    private void Transition(Action<DateTimeOffset> change)
    {
        change(_timeProvider.GetUtcNow());

        if (_timer.IsRunning)
        {
            StartTicking();
        }
        else
        {
            StopTicking();
        }

        Redraw();
        _onTransition();
    }

    private void StartTicking()
    {
        _ticker ??= _timeProvider.CreateTimer(
            _ => _ui.Post(Tick),
            state: null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// One second of running. Stops the ticker once a countdown has finished:
    /// the display reads 0:00 and cannot change again, so waking up to redraw it
    /// is work nobody asked for - the same reason a stopped timer does not tick.
    /// Nothing stored changes here, so this still asks for no write.
    /// </summary>
    private void Tick()
    {
        Redraw();

        if (_timer.HasFinishedAt(_timeProvider.GetUtcNow()))
        {
            StopTicking();
        }
    }

    private void StopTicking()
    {
        _ticker?.Dispose();
        _ticker = null;
    }

    private void Redraw()
    {
        if (_disposed)
        {
            return;
        }

        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasFinished));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(DurationMinutes));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(IsCountingDown));
        OnPropertyChanged(nameof(CanEdit));
    }
}
