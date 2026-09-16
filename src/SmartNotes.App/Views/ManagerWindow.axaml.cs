using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SmartNotes.ViewModels;

namespace SmartNotes.App.Views;

public partial class ManagerWindow : Window
{
    private ManagerViewModel? _manager;

    public ManagerWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // Searching and switching to the archive are driven by the view-model's
        // own properties changing, not by these controls' events - see
        // ManagerViewModel. Wiring them here would race the bindings.

        // The list is rebuilt whenever the window is brought forward. A note
        // window and this share nothing but the database, so refreshing on
        // activation is simpler and harder to get wrong than a
        // change-notification web between them; the cost is that a note edited
        // elsewhere keeps its old preview until you come back here.
        Activated += (_, _) => Refresh();
    }

    public void Bind(ManagerViewModel manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _manager = manager;
        DataContext = manager;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Refresh();
    }

    private void Refresh()
    {
        if (_manager is not null)
        {
            _ = _manager.RefreshAsync();
        }
    }
}
