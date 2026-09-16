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

    /// <summary>
    /// How long a countdown runs for, in whole minutes.
    /// </summary>
    /// <remarks>
    /// Held at one minute or more: a countdown of zero is finished before it is
    /// started, which looks like a bug rather than a choice.
    /// </remarks>
    public int DurationMinutes
    {
        get => (int)_timer.Duration.TotalMinutes;
        set => Change(
            Math.Max(ShortestMinutes, value),
            DurationMinutes,
            v => _timer.Duration = TimeSpan.FromMinutes(v));
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
    public void SetDuration(int minutes) => DurationMinutes = minutes;

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
            _ => _ui.Post(Redraw),
            state: null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
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
        OnPropertyChanged(nameof(IsCountingDown));
        OnPropertyChanged(nameof(CanEdit));
    }
}
