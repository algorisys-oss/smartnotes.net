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

    public string Label => _timer.Label;

    public string Display => _timer.DisplayAt(_timeProvider.GetUtcNow());

    public bool IsRunning => _timer.IsRunning;

    public bool HasFinished => _timer.HasFinishedAt(_timeProvider.GetUtcNow());

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
    }
}
