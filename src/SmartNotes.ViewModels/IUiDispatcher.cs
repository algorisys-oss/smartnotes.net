namespace SmartNotes.ViewModels;

/// <summary>
/// Gets work back onto the thread the UI is drawn on.
/// </summary>
/// <remarks>
/// <see cref="TimeProvider.CreateTimer"/> is BCL, so a view-model may tick
/// without reaching for Avalonia's DispatcherTimer - but its callback arrives on
/// a thread-pool thread, and a binding has to be updated on the UI one. This is
/// that seam, the same shape as <see cref="IWindowManager"/>.
/// </remarks>
public interface IUiDispatcher
{
    void Post(Action action);
}
