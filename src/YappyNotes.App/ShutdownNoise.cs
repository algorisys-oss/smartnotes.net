namespace YappyNotes.App;

/// <summary>
/// The one kind of exception reaching the dispatcher that the app lets pass.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia 12.1.2's Linux tray (<c>DBusTrayIconImpl.Dispose</c>) cancels its D-Bus
/// watch before setting the flag its own <c>catch</c> filters check, so the
/// cancellation escapes an <c>async void</c> onto the dispatcher and aborts the
/// process - on quit, whenever the loop runs one more job after the tray is gone.
/// It is a race: eight quits in a row did not show it, disposing the tray while
/// the app ran showed it every time.
/// </para>
/// <para>
/// Only a cancellation, and only once shutdown has begun, by which point
/// <c>ShutdownRequested</c> has already written the notes. Anything else still
/// crashes: a failure hidden here would be a note lost without a word. Remove this
/// once Avalonia's tray sets the flag first.
/// </para>
/// </remarks>
public static class ShutdownNoise
{
    public static bool IsHarmless(Exception exception, bool shuttingDown)
        => shuttingDown && exception is OperationCanceledException;
}
