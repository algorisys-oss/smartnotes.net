using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

/// <summary>
/// Builds the tray icon and its menu.
/// </summary>
/// <remarks>
/// In code rather than in <c>App.axaml</c>: a <see cref="TrayIcon"/> is not in the
/// visual tree, so it inherits no data context and a binding on it resolves to
/// nothing, silently. Setting each command directly is the version that cannot
/// quietly come unwired.
/// </remarks>
public static class TrayMenu
{
    public static TrayIcon Create(TrayViewModel tray)
    {
        ArgumentNullException.ThrowIfNull(tray);

        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("New note") { Command = tray.NewNoteCommand });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Show all notes") { Command = tray.ShowAllNotesCommand });
        menu.Items.Add(new NativeMenuItem("Hide all notes") { Command = tray.HideAllNotesCommand });
        menu.Items.Add(new NativeMenuItem("Open manager") { Command = tray.OpenManagerCommand });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Quit") { Command = tray.QuitCommand });

        return new TrayIcon
        {
            Icon = AppIcon.Load(),
            ToolTipText = "YappyNotes",
            Command = tray.OpenManagerCommand,
            Menu = menu,
        };
    }
}

/// <summary>The app's icon, shared by the tray and the windows.</summary>
public static class AppIcon
{
    private static readonly Uri Source = new("avares://YappyNotes.App/Assets/yappynotes.png");

    public static WindowIcon Load()
    {
        using var stream = AssetLoader.Open(Source);
        return new WindowIcon(new Bitmap(stream));
    }
}
