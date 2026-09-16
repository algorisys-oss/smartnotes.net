using System.ComponentModel;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

/// <summary>
/// The tray's update item. It is one menu entry whose words and action follow
/// the state, because a tray menu is a list you glance at, not a dialog.
/// </summary>
public class UpdatesViewModelTests
{
    private readonly FakeUpdater _updater = new();
    private readonly FakeAppLifetime _lifetime = new();

    private UpdatesViewModel NewUpdates() => new(_updater, _lifetime);

    [Fact]
    public void MenuText_BeforeAnyCheck_OffersOne()
    {
        var updates = NewUpdates();

        Assert.Equal("Check for updates", updates.MenuText);
    }

    /// <summary>
    /// Run from source or from an unpacked archive there is nothing to replace, so
    /// the item says why instead of checking and then failing to install.
    /// </summary>
    [Fact]
    public void UpdateCommand_WhereUpdatesCannotBeInstalled_CannotBeRun()
    {
        _updater.CanUpdate = false;

        var updates = NewUpdates();

        Assert.False(updates.UpdateCommand.CanExecute(null));
        Assert.Equal("Updates come with the installed app", updates.MenuText);
    }

    [Fact]
    public async Task UpdateCommand_WhenThisIsTheLatest_SaysSo()
    {
        var updates = NewUpdates();

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Equal("Up to date", updates.MenuText);
        Assert.Equal(0, _updater.Downloads);
    }

    [Fact]
    public async Task UpdateCommand_WhenANewerReleaseExists_DownloadsIt()
    {
        _updater.Newer = "0.3.0";
        var updates = NewUpdates();

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, _updater.Downloads);
    }

    [Fact]
    public async Task UpdateCommand_OnceDownloaded_OffersARestartNamingTheVersion()
    {
        _updater.Newer = "0.3.0";
        var updates = NewUpdates();

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Equal("Restart to update to 0.3.0", updates.MenuText);
    }

    /// <summary>
    /// The installer is told to wait for this process first, and only then does
    /// the app quit - through the lifetime, which is the shutdown that saves
    /// whatever the debounce still holds. Exiting some other way would lose the
    /// last thing typed to an update.
    /// </summary>
    [Fact]
    public async Task UpdateCommand_WhenReadyToRestart_ArrangesTheInstallBeforeQuitting()
    {
        _updater.Newer = "0.3.0";
        var updates = NewUpdates();
        await updates.UpdateCommand.ExecuteAsync(null);
        var arrangedBeforeQuit = false;
        _lifetime.OnQuit = () => arrangedBeforeQuit = _updater.AppliedOnExit;

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, _lifetime.QuitRequests);
        Assert.True(arrangedBeforeQuit);
    }

    [Fact]
    public async Task UpdateCommand_WhenReadyToRestart_DoesNotCheckAgain()
    {
        _updater.Newer = "0.3.0";
        var updates = NewUpdates();
        await updates.UpdateCommand.ExecuteAsync(null);

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, _updater.Checks);
    }

    /// <summary>
    /// Offline is normal for a laptop. A failed check is a line in a menu, never
    /// an exception on a thread nobody is watching.
    /// </summary>
    [Fact]
    public async Task UpdateCommand_WhenTheCheckFails_SaysSoRatherThanThrowing()
    {
        _updater.CheckFailure = new HttpRequestException("offline");
        var updates = NewUpdates();

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Equal("Could not check for updates - try again", updates.MenuText);
        Assert.True(updates.UpdateCommand.CanExecute(null));
    }

    [Fact]
    public async Task UpdateCommand_WhileAlreadyChecking_CannotBeRunAgain()
    {
        _updater.HoldCheck = new TaskCompletionSource();
        var updates = NewUpdates();

        var running = updates.UpdateCommand.ExecuteAsync(null);

        Assert.False(updates.UpdateCommand.CanExecute(null));
        Assert.Equal("Checking for updates...", updates.MenuText);
        _updater.HoldCheck.SetResult();
        await running;
    }

    /// <summary>The tray menu repaints its label from this, so it has to be announced.</summary>
    [Fact]
    public async Task MenuText_WhenTheStateMoves_IsAnnounced()
    {
        var updates = NewUpdates();
        var announced = new List<string?>();
        updates.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        await updates.UpdateCommand.ExecuteAsync(null);

        Assert.Contains(nameof(UpdatesViewModel.MenuText), announced);
    }

    [Fact]
    public async Task CheckOnStartAsync_WithTheSettingOn_Checks()
    {
        var updates = NewUpdates();

        await updates.CheckOnStartAsync(checkForUpdates: true);

        Assert.Equal(1, _updater.Checks);
    }

    [Fact]
    public async Task CheckOnStartAsync_WithTheSettingOff_DoesNotAsk()
    {
        var updates = NewUpdates();

        await updates.CheckOnStartAsync(checkForUpdates: false);

        Assert.Equal(0, _updater.Checks);
    }

    [Fact]
    public async Task CheckOnStartAsync_WhereUpdatesCannotBeInstalled_DoesNotAsk()
    {
        _updater.CanUpdate = false;
        var updates = NewUpdates();

        await updates.CheckOnStartAsync(checkForUpdates: true);

        Assert.Equal(0, _updater.Checks);
    }
}
