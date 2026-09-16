using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SmartNotes.ViewModels;

namespace SmartNotes.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => AvaloniaXamlLoader.Load(this);

    public SettingsWindow(SettingsViewModel settings) : this() => DataContext = settings;
}
