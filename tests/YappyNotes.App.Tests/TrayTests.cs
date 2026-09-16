using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

/// <summary>
/// Living in the tray: the app outlives its windows, so the manager has to come
/// back after being closed, and quitting has to be the kind of shutdown that
/// still saves.
/// </summary>
public class TrayTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;
    private readonly WindowManager _windows;

    private readonly FakeUpdater _updater = new();

    private UpdatesViewModel NewUpdates() => new(_updater, new FakeAppLifetime());

    public TrayTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
        _windows = new WindowManager(_notes, _autoSave);
    }

    [AvaloniaFact]
    public void ShowManager_CalledTwice_OpensOneManagerWindow()
    {
        _windows.ShowManager();
        var first = _windows.OpenManager;
        _windows.ShowManager();

        Assert.NotNull(first);
        Assert.Same(first, _windows.OpenManager);
    }

    /// <summary>
    /// A closed Avalonia window cannot be shown again, and the manager used to be
    /// the window whose closing ended the app. Now the app keeps running, so the
    /// tray has to be able to get a fresh one.
    /// </summary>
    [AvaloniaFact]
    public void ShowManager_AfterTheManagerWasClosed_OpensANewOne()
    {
        _windows.ShowManager();
        var first = _windows.OpenManager!;
        first.Close();

        _windows.ShowManager();

        Assert.NotNull(_windows.OpenManager);
        Assert.NotSame(first, _windows.OpenManager);
        Assert.True(_windows.OpenManager!.IsVisible);
    }

    /// <summary>
    /// A note window calls its own close off to flush first. During an app
    /// shutdown, a close that is still called off when <c>TryShutdown</c> looks
    /// makes it give up - verified with a spike - and with the app living in the
    /// tray nothing else would end the process. The flush has already happened by
    /// then: <c>ShutdownRequested</c> is raised before any window is asked to
    /// close, and that handler disposes the autosave.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(WindowCloseReason.ApplicationShutdown)]
    [InlineData(WindowCloseReason.OSShutdown)]
    public void NoteWindow_ClosingBecauseTheAppIsEnding_DoesNotHoldTheCloseUp(WindowCloseReason reason)
    {
        Assert.False(NoteWindow.HoldsCloseToFlush(reason));
    }

    [AvaloniaFact]
    public void NoteWindow_ClosedByTheReader_HoldsTheCloseUpToFlush()
    {
        Assert.True(NoteWindow.HoldsCloseToFlush(WindowCloseReason.WindowClosing));
    }

    /// <summary>
    /// <c>Shutdown()</c> skips <c>ShutdownRequested</c>, which is where the notes
    /// still sitting in a debounce are written; <c>TryShutdown()</c> raises it.
    /// Both end the app, so only a test tells them apart. The request is cancelled
    /// here so that nothing actually shuts down.
    /// </summary>
    [AvaloniaFact]
    public void DesktopAppLifetime_Quit_RaisesTheShutdownRequestThatSaves()
    {
        var desktop = new ClassicDesktopStyleApplicationLifetime();
        var requested = 0;
        desktop.ShutdownRequested += (_, e) =>
        {
            requested++;
            e.Cancel = true;
        };

        new DesktopAppLifetime(desktop).Quit();

        Assert.Equal(1, requested);
    }

    [AvaloniaFact]
    public void TrayMenu_ForATray_OffersEachItemInOrder()
    {
        var tray = new TrayViewModel(_notes, _windows, new FakeAppLifetime());

        var icon = TrayMenu.Create(tray, NewUpdates());

        var headers = icon.Menu!.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).Select(i => i.Header);
        Assert.Equal(["New note", "Show all notes", "Hide all notes", "Open manager", "Check for updates", "Quit"], headers);
    }

    [AvaloniaFact]
    public void TrayMenu_ForATray_WiresEachItemToItsCommand()
    {
        var tray = new TrayViewModel(_notes, _windows, new FakeAppLifetime());
        var updates = NewUpdates();

        var icon = TrayMenu.Create(tray, updates);

        var commands = icon.Menu!.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).Select(i => i.Command);
        Assert.Equal(
            [tray.NewNoteCommand, tray.ShowAllNotesCommand, tray.HideAllNotesCommand, tray.OpenManagerCommand, updates.UpdateCommand, tray.QuitCommand],
            commands);
    }

    /// <summary>
    /// A plain click on the icon, as opposed to opening its menu. Not every
    /// platform raises it - some only ever show the menu - so it is a shortcut
    /// and the menu still has to carry Open manager.
    /// </summary>
    [AvaloniaFact]
    public void TrayMenu_ClickingTheIcon_OpensTheManager()
    {
        var tray = new TrayViewModel(_notes, _windows, new FakeAppLifetime());

        var icon = TrayMenu.Create(tray, NewUpdates());

        Assert.Same(tray.OpenManagerCommand, icon.Command);
    }

    [AvaloniaFact]
    public void TrayMenu_ForATray_HasAnIcon()
    {
        var tray = new TrayViewModel(_notes, _windows, new FakeAppLifetime());

        var icon = TrayMenu.Create(tray, NewUpdates());

        Assert.NotNull(icon.Icon);
    }

    /// <summary>
    /// A NativeMenuItem is not in the visual tree and binds to nothing, so the
    /// label is copied across whenever it changes - and a check that finished
    /// with the menu still saying "Check for updates" would look like it never ran.
    /// </summary>
    [AvaloniaFact]
    public async Task TrayMenu_WhenAnUpdateIsReady_RelabelsTheUpdateItem()
    {
        _updater.Newer = "0.3.0";
        var tray = new TrayViewModel(_notes, _windows, new FakeAppLifetime());
        var updates = NewUpdates();
        var icon = TrayMenu.Create(tray, updates);

        await updates.UpdateCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var headers = icon.Menu!.Items.OfType<NativeMenuItem>().Select(i => i.Header);
        Assert.Contains("Restart to update to 0.3.0", headers);
    }

    /// <summary>
    /// The test run is a build, not an install, which is exactly the case that
    /// has to answer "cannot update" without going anywhere near the network.
    /// </summary>
    [Fact]
    public void VelopackUpdater_RunFromABuild_CannotUpdate()
    {
        var updater = new VelopackUpdater();

        Assert.False(updater.CanUpdate);
    }

    [Fact]
    public async Task VelopackUpdater_RunFromABuild_FindsNothingToInstall()
    {
        var updater = new VelopackUpdater();

        Assert.Null(await updater.CheckAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Someone on a beta should be offered the next beta; everyone else should
    /// never be moved onto one.
    /// </summary>
    [Theory]
    [InlineData("0.2.0", false)]
    [InlineData("0.2.0-beta.1", true)]
    public void VelopackUpdater_ForAVersion_LooksAtPrereleasesOnlyFromAPrerelease(string version, bool expected)
    {
        Assert.Equal(expected, VelopackUpdater.WantsPrereleases(version));
    }
}
