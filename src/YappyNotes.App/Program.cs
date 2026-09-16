using Avalonia;

namespace YappyNotes.App;

internal static class Program
{
    // Don't touch Avalonia, or anything relying on a SynchronizationContext,
    // before BuildAvaloniaApp runs - none of it is initialised yet.
    [STAThread]
    public static int Main(string[] args)
    {
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
