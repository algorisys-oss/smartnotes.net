using Avalonia.Headless.XUnit;
using SmartNotes.App.Views;

namespace SmartNotes.App.Tests;

public class ScaffoldTests
{
    /// <summary>
    /// Proves the headless harness is wired up, and that the XAML actually
    /// compiles and loads - a broken .axaml is a runtime failure in Avalonia, not
    /// a build one, so nothing else in this repository would have caught it.
    /// </summary>
    [AvaloniaFact]
    public void Scaffold_TheManagerWindow_Opens()
    {
        var window = new ManagerWindow();

        window.Show();

        Assert.True(window.IsVisible);
    }
}
