using Avalonia.Controls;
using SmartNotes.Core;
using SmartNotes.ViewModels;

namespace SmartNotes.App;

/// <summary>
/// Opens a link through the window it was clicked in.
/// </summary>
/// <remarks>
/// <para>
/// Takes the window as a callback rather than a reference, because the launcher
/// is built with the view-model and the window does not exist yet at that point.
/// </para>
/// <para>
/// The allow-list is checked here as well as where the link was found. That is
/// not tidy-up-later duplication: this is the one line where a string out of a
/// note reaches the operating system's shell, and the check belongs where the
/// danger is.
/// </para>
/// </remarks>
public sealed class AvaloniaLinkLauncher : ILinkLauncher
{
    private readonly Func<TopLevel?> _owner;

    public AvaloniaLinkLauncher(Func<TopLevel?> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<bool> OpenAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!LinkScanner.IsAllowed(uri) || _owner() is not { Launcher: { } launcher })
        {
            return false;
        }

        return await launcher.LaunchUriAsync(uri);
    }
}
