using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => AvaloniaXamlLoader.Load(this);

    public SettingsWindow(SettingsViewModel settings) : this() => DataContext = settings;
}
