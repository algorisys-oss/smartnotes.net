using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using YappyNotes.Core;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

public partial class App : Application
{
    private AppServices? _services;
    private WindowManager? _windows;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // The headless test harness brings the Application up without a desktop
        // lifetime, so this has to stay conditional rather than assume a window.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The app lives in the tray and runs all day, so closing the manager
            // - or every note - must not end it. Quitting is the tray's Quit, which
            // goes through TryShutdown so that ShutdownRequested still flushes.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += OnShutdownRequested;

            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            _services = await AppServices.StartAsync(UserPaths.Resolve());
        }
        catch
        {
            // With an explicit shutdown mode nothing else would ever end the
            // process: no window has opened and no tray icon exists yet, so a
            // failed start would leave YappyNotes running with nothing to click.
            desktop.Shutdown(1);
            throw;
        }

        _windows = new WindowManager(
            _services.Notes,
            _services.AutoSave,
            () => new SettingsViewModel(_services!.Settings, new ThemeApplier()));

        new ThemeApplier().Apply((await _services.Settings.LoadAsync()).Theme);

        var tray = new TrayViewModel(_services.Notes, _windows, new DesktopAppLifetime(desktop));
        TrayIcon.SetIcons(this, [TrayMenu.Create(tray)]);

        _windows.ShowManager();

        // Restore note windows: every note that was on the desktop comes back
        // where it was left.
        foreach (var note in await _services.Notes.GetActiveAsync())
        {
            await _windows.ShowNoteAsync(note.Id);
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        // Everything still sitting in a debounce gets written before the process
        // goes. Blocking here is the price of shutdown being synchronous, and it
        // is a handful of small UPDATEs.
        _services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _services = null;
    }
}
