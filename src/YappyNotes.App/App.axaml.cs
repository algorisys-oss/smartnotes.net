using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YappyNotes.App.Views;
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
            var manager = new ManagerWindow();
            desktop.MainWindow = manager;

            // Closing the last note window must not end the process - the notes
            // are the app, and the manager is how you get another one.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            desktop.ShutdownRequested += OnShutdownRequested;

            _ = StartAsync(manager);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(ManagerWindow manager)
    {
        _services = await AppServices.StartAsync(UserPaths.Resolve());
        _windows = new WindowManager(
            _services.Notes,
            _services.AutoSave,
            () => new SettingsViewModel(_services!.Settings, new ThemeApplier()));

        new ThemeApplier().Apply((await _services.Settings.LoadAsync()).Theme);

        manager.Bind(new ManagerViewModel(_services.Notes, _windows));

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
