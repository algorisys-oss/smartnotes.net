using Avalonia.Threading;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

/// <summary>Gets a timer tick back onto the thread Avalonia draws on.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
