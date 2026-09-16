using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using YappyNotes.App;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Views;

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

        // Set here rather than bound: the version belongs to the app's assembly,
        // and a view-model that had to know it would be a view-model that knows
        // which program it is running in.
        var version = this.FindControl<TextBlock>("StatusVersion");
        if (version is not null)
        {
            version.Text = AppVersion.Current;
        }

        manager.Items.CollectionChanged += (_, _) => ShowCount();
        ShowCount();
    }

    private void ShowCount()
    {
        var count = this.FindControl<TextBlock>("StatusCount");
        if (count is null || _manager is null)
        {
            return;
        }

        var total = _manager.Items.Count;
        var what = _manager.ShowingArchive ? "archived" : "on the desktop";

        count.Text = total == 1 ? $"1 note {what}" : $"{total} notes {what}";
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
