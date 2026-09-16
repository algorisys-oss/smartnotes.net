using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YappyNotes.ViewModels;

/// <summary>
/// The tray's update item: check, download, and restart into the new version.
/// </summary>
/// <remarks>
/// <para>
/// One menu entry whose words and action follow the state, rather than a dialog.
/// A newer release is downloaded as soon as it is found, without asking, so that
/// the choice the reader is offered is the only one that matters: restart now, or
/// carry on. Quitting without restarting is also fine - the installer applies a
/// downloaded release on the next start.
/// </para>
/// <para>
/// Restarting goes through <see cref="IAppLifetime.Quit"/> and not an installer's
/// own exit-and-apply. That is the shutdown that writes what the autosave is
/// still holding; the installer's exits the process where it stands.
/// </para>
/// </remarks>
public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly IUpdater _updater;
    private readonly IAppLifetime _lifetime;

    public UpdatesViewModel(IUpdater updater, IAppLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(lifetime);

        _updater = updater;
        _lifetime = lifetime;
        State = updater.CanUpdate ? UpdateState.NotChecked : UpdateState.NotInstallable;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MenuText))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    public partial UpdateState State { get; private set; }

    /// <summary>The version downloaded and waiting for a restart, once there is one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MenuText))]
    public partial string? NewVersion { get; private set; }

    public string MenuText => State switch
    {
        UpdateState.NotInstallable => "Updates come with the installed app",
        UpdateState.Checking => "Checking for updates...",
        UpdateState.UpToDate => "Up to date",
        UpdateState.Downloading => $"Downloading {NewVersion}...",
        UpdateState.ReadyToRestart => $"Restart to update to {NewVersion}",
        UpdateState.Failed => "Could not check for updates - try again",
        _ => "Check for updates",
    };

    /// <summary>
    /// The check made on start, if the reader has not turned it off. A copy that
    /// could not install an update does not ask whether there is one.
    /// </summary>
    public Task CheckOnStartAsync(bool checkForUpdates)
        => checkForUpdates && CanUpdate() ? UpdateAsync() : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    public async Task UpdateAsync()
    {
        if (State == UpdateState.ReadyToRestart)
        {
            _updater.ApplyOnExit();
            _lifetime.Quit();
            return;
        }

        try
        {
            State = UpdateState.Checking;
            var newer = await _updater.CheckAsync();
            if (newer is null)
            {
                State = UpdateState.UpToDate;
                return;
            }

            NewVersion = newer;
            State = UpdateState.Downloading;
            await _updater.DownloadAsync();
            State = UpdateState.ReadyToRestart;
        }
        catch (Exception)
        {
            // Offline is normal for a laptop, and GitHub rate-limits anonymous
            // callers. Neither is worth more than a line in the menu.
            NewVersion = null;
            State = UpdateState.Failed;
        }
    }

    private bool CanUpdate() => State is not (UpdateState.NotInstallable or UpdateState.Checking or UpdateState.Downloading);
}

public enum UpdateState
{
    /// <summary>Not installed by a release installer, so nothing could be replaced.</summary>
    NotInstallable,
    NotChecked,
    Checking,
    UpToDate,
    Downloading,
    ReadyToRestart,
    Failed,
}
