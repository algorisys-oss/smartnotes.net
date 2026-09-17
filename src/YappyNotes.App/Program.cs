using Avalonia;
using Velopack;
using YappyNotes.Core;

namespace YappyNotes.App;

internal static class Program
{
    // Don't touch Avalonia, or anything relying on a SynchronizationContext,
    // before BuildAvaloniaApp runs - none of it is initialised yet.
    [STAThread]
    public static int Main(string[] args)
    {
        // A normal start claims the notes folder before anything else, and a start
        // that finds it claimed wakes the running copy and leaves. Before Velopack
        // too: its auto-apply would otherwise install an update over the files of
        // the copy that is running. Only a start with no arguments - an installer's
        // hooks and --version never open the notes, so they claim nothing.
        using var runningCopy = args.Length == 0 ? ClaimTheNotesFolder() : null;
        if (args.Length == 0 && runningCopy is null)
        {
            return WakeTheCopyAlreadyRunning();
        }

        // First of what reads the arguments. An installer starts the app
        // with its own (--veloapp-install and friends) to run a hook and exit, and
        // CommandLine would answer those as unknown arguments with exit code 2.
        // On a normal start it also installs an update that was downloaded but
        // never restarted into.
        var velopack = VelopackApp.Build()
            .SetAutoApplyOnStartup(CommandLine.MayApplyUpdates(args));

        // Windows is the only platform whose uninstaller runs anything of ours, and
        // the Run key is the one registration that would otherwise go on naming a
        // deleted program. Linux's entry has TryExec for that.
        if (OperatingSystem.IsWindows())
        {
            velopack = velopack.OnBeforeUninstallFastCallback(_ => WindowsRunKey.Remove());
        }

        velopack.Run();

        // Before Avalonia, so that a question can be answered on a machine with
        // no display - which is how the release workflow proves the packaged
        // binary runs on Windows and macOS.
        if (CommandLine.TryAnswer(args, out var output, out var exitCode))
        {
            Console.WriteLine(output);
            return exitCode;
        }

        return BuildAvaloniaApp(() => new App { RunningCopy = runningCopy })
            .StartWithClassicDesktopLifetime(args);
    }

    private static SingleInstance? ClaimTheNotesFolder()
    {
        var paths = UserPaths.Resolve();

        // The lock file lives in the folder, so the folder has to exist - and the
        // notes kept under the app's old name have to be adopted before it does,
        // or they are stranded beside an empty new one. AppServices does both
        // again later, which finds nothing left to do.
        paths.AdoptLegacyDirectory();
        paths.EnsureCreated();

        return SingleInstance.TryClaim(paths.DataDirectory);
    }

    private static int WakeTheCopyAlreadyRunning()
    {
        var paths = UserPaths.Resolve();

        // Generous, because the running copy only listens once it has migrated its
        // database and restored its notes, and a double-click on the launcher
        // arrives while it is still doing that.
        if (SingleInstance.TryWakeRunningCopy(paths.DataDirectory, TimeSpan.FromSeconds(15)))
        {
            return 0;
        }

        // Never start anyway. A copy that holds the folder and does not answer is
        // still a copy holding its own notes, and a second one is the thing this
        // exists to prevent.
        Console.Error.WriteLine(
            $"YappyNotes is already running for {paths.DataDirectory} but did not answer. "
            + "Quit it from the tray, or end the process, and start it again.");
        return 1;
    }

    // Also called by the visual designer and by the headless test harness, which
    // is why it is public and why it does not start anything itself.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(() => new App());

    private static AppBuilder BuildAvaloniaApp(Func<App> app)
        => AppBuilder.Configure(app)
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
