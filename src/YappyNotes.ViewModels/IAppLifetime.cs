namespace YappyNotes.ViewModels;

/// <summary>
/// Ends the running application.
/// </summary>
/// <remarks>
/// A seam for the same reason as <see cref="IWindowManager"/>: the tray's Quit
/// item has to be testable without an Avalonia lifetime existing. The
/// implementation is where the one thing worth knowing lives - which of
/// Avalonia's two shutdown calls still saves what the debounce is holding.
/// </remarks>
public interface IAppLifetime
{
    void Quit();
}
