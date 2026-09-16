using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
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
    public static TrayIcon Create(TrayViewModel tray, UpdatesViewModel updates)
    {
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(updates);

        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("New note") { Command = tray.NewNoteCommand });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Show all notes") { Command = tray.ShowAllNotesCommand });
        menu.Items.Add(new NativeMenuItem("Hide all notes") { Command = tray.HideAllNotesCommand });
        menu.Items.Add(new NativeMenuItem("Open manager") { Command = tray.OpenManagerCommand });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(UpdateItem(updates));
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

    /// <remarks>
    /// Its label is copied over on every change, for the same reason the commands
    /// are set rather than bound. The change can arrive after an await that
    /// resumed off the UI thread, and a native menu is not safe to touch from one.
    /// </remarks>
    private static NativeMenuItem UpdateItem(UpdatesViewModel updates)
    {
        var item = new NativeMenuItem(updates.MenuText) { Command = updates.UpdateCommand };

        updates.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdatesViewModel.MenuText))
            {
                Dispatcher.UIThread.Post(() => item.Header = updates.MenuText);
            }
        };

        return item;
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
