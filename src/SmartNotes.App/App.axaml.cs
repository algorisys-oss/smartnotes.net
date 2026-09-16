using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SmartNotes.App.Views;

namespace SmartNotes.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // The headless test harness brings the Application up without a desktop
        // lifetime, so this has to stay conditional rather than assume a window.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ManagerWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
