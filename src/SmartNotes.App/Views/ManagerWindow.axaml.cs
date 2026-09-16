using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SmartNotes.App.Views;

public partial class ManagerWindow : Window
{
    /// <summary>
    /// What pressing "New note" does. Set by the bootstrap, because the manager
    /// has no view-model of its own until Milestone 3.
    /// </summary>
    public Func<Task>? NewNote { get; set; }

    public ManagerWindow() => AvaloniaXamlLoader.Load(this);

    private void OnNewNoteClicked(object? sender, RoutedEventArgs e)
    {
        if (NewNote is not null)
        {
            _ = NewNote();
        }
    }
}
