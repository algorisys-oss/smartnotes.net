using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>
/// What a start does about the login item: makes the system match the setting.
/// </summary>
/// <remarks>
/// On every start rather than only when the checkbox changes. The setting is on by
/// default, so the first start of an installed copy is what registers it; and an
/// app that has been moved - an AppImage to another folder, a .app into
/// /Applications - gets its entry rewritten with where it is now.
/// </remarks>
public static class LoginStartup
{
    public static async Task ApplyStoredAsync(
        SettingsService settings,
        ILoginItem loginItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loginItem);

        if (!loginItem.IsAvailable)
        {
            return;
        }

        var stored = await settings.LoadAsync(cancellationToken);

        try
        {
            loginItem.Set(stored.StartAtLogin);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // This runs on the way to restoring the notes. Failing to register
            // for login is a reason to start without it, never a reason not to
            // start; the checkbox is where somebody finds out.
        }
    }
}
