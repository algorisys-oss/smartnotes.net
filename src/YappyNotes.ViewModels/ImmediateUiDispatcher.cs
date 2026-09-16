namespace YappyNotes.ViewModels;

/// <summary>
/// Runs the work straight away, on whatever thread posted it.
/// </summary>
/// <remarks>
/// The default for a view-model built without one - which is every test that has
/// nothing to do with timers. The app supplies a real dispatcher; using this one
/// where a UI is actually running would update a binding off-thread.
/// </remarks>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
