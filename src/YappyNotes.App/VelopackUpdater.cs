using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

/// <summary>
/// Updates from the project's GitHub releases, through Velopack.
/// </summary>
/// <remarks>
/// <para>
/// Only a copy installed by a Velopack installer can update: it is the installer
/// that leaves behind the updater and the record of which release and channel
/// this is. A build, an unpacked archive and <c>scripts/deploy-local.sh</c>'s
/// folder all answer <see cref="CanUpdate"/> with false and never go to the
/// network.
/// </para>
/// <para>
/// The channel is not chosen here. <c>vpk pack --channel</c> records it in the
/// install, one per runtime identifier, so a linux-arm64 install is only ever
/// offered a linux-arm64 release.
/// </para>
/// <para>
/// <c>YAPPYNOTES_UPDATE_SOURCE</c> replaces GitHub with a folder of releases, so an
/// update can be tried end to end on one machine without publishing anything.
/// </para>
/// </remarks>
public sealed class VelopackUpdater : IUpdater
{
    private const string Repository = "https://github.com/algorisys-oss/yappynotes";

    private readonly UpdateManager _manager;
    private UpdateInfo? _found;

    public VelopackUpdater()
    {
        var local = Environment.GetEnvironmentVariable("YAPPYNOTES_UPDATE_SOURCE");
        IUpdateSource source = string.IsNullOrWhiteSpace(local)
            ? new GithubSource(Repository, accessToken: null, WantsPrereleases(AppVersion.Current))
            : new SimpleFileSource(new DirectoryInfo(local));

        // Main runs VelopackApp first, which sets the locator. Anything that builds
        // one of these without that - the tests - gets the platform's default
        // instead of an exception, and the default knows it is not installed.
        var locator = VelopackLocator.IsCurrentSet ? null : VelopackLocator.CreateDefaultForPlatform();
        _manager = new UpdateManager(source, options: null, locator);
    }

    public bool CanUpdate => _manager.IsInstalled;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUpdate)
        {
            return null;
        }

        _found = await _manager.CheckForUpdatesAsync();
        return _found?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (_found is null)
        {
            throw new InvalidOperationException("There is nothing to download until a check has found a release.");
        }

        await _manager.DownloadUpdatesAsync(_found, cancelToken: cancellationToken);
    }

    /// <remarks>
    /// Silent, because the reader already chose to restart from the tray, and
    /// with a restart, because that is what they chose. The updater waits for this
    /// process to exit - up to a minute - which is what lets the exit be the
    /// app's own flushing shutdown instead of Velopack's immediate one.
    /// </remarks>
    public void ApplyOnExit()
    {
        if (_found is null)
        {
            throw new InvalidOperationException("There is nothing to install until a release has been downloaded.");
        }

        _manager.WaitExitThenApplyUpdates(_found.TargetFullRelease, silent: true, restart: true);
    }

    /// <summary>
    /// Whether a copy of this version should be offered prereleases. Someone on a
    /// beta gets the next beta; nobody else is ever moved onto one.
    /// </summary>
    public static bool WantsPrereleases(string version) => version.Contains('-', StringComparison.Ordinal);
}
