using Avalonia;
using Velopack;

namespace YappyNotes.App;

internal static class Program
{
    // Don't touch Avalonia, or anything relying on a SynchronizationContext,
    // before BuildAvaloniaApp runs - none of it is initialised yet.
    [STAThread]
    public static int Main(string[] args)
    {
        // First, before anything reads the arguments. An installer starts the app
        // with its own (--veloapp-install and friends) to run a hook and exit, and
        // CommandLine would answer those as unknown arguments with exit code 2.
        // On a normal start it also installs an update that was downloaded but
        // never restarted into.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(CommandLine.MayApplyUpdates(args))
            .Run();

        // Before Avalonia, so that a question can be answered on a machine with
        // no display - which is how the release workflow proves the packaged
        // binary runs on Windows and macOS.
        if (CommandLine.TryAnswer(args, out var output, out var exitCode))
        {
            Console.WriteLine(output);
            return exitCode;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also called by the visual designer and by the headless test harness, which
    // is why it is public and why it does not start anything itself.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
