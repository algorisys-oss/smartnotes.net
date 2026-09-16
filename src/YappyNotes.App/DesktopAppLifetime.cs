using Avalonia.Controls.ApplicationLifetimes;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

/// <summary>
/// Quits through Avalonia's desktop lifetime.
/// </summary>
/// <remarks>
/// <c>TryShutdown</c>, never <c>Shutdown</c>. Both end the app, but only
/// <c>TryShutdown</c> raises <c>ShutdownRequested</c>, and that is where the notes
/// still sitting in a debounce are written. Checked with a spike rather than read
/// off the names: <c>Shutdown</c> goes straight to <c>Exit</c>.
/// </remarks>
public sealed class DesktopAppLifetime(IClassicDesktopStyleApplicationLifetime desktop) : IAppLifetime
{
    public void Quit() => desktop.TryShutdown();
}
