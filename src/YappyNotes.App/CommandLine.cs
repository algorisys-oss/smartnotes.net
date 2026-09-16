using System.Text;
using YappyNotes.Core;

namespace YappyNotes.App;

/// <summary>
/// The few arguments YappyNotes answers without opening a window.
/// </summary>
/// <remarks>
/// It is a desktop app and not a CLI, so this stays small on purpose. It exists
/// because the release workflow has to prove the packaged binary runs on Windows
/// and macOS, and a runner has no display to open into: answering an argument and
/// exiting is the proof. `--where` earns its place separately, for the
/// XDG_DATA_HOME surprise the README documents.
/// </remarks>
public static class CommandLine
{
    /// <summary>
    /// Answers <paramref name="args"/> if it is something to answer rather than a
    /// normal start.
    /// </summary>
    /// <returns>True if the app should print <paramref name="output"/> and exit.</returns>
    public static bool TryAnswer(string[] args, out string output, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(args);

        output = string.Empty;
        exitCode = 0;

        if (args.Length == 0)
        {
            return false;
        }

        switch (args[0])
        {
            case "--version" or "-v":
                output = $"YappyNotes {AppVersion.Current}";
                return true;

            case "--help" or "-h":
                output = Help;
                return true;

            case "--where":
                var paths = UserPaths.Resolve();
                output = paths.DatabaseFile;
                return true;

            default:
                output = $"YappyNotes does not know the argument '{args[0]}'.\n\n{Help}";
                exitCode = 2;
                return true;
        }
    }


    /// <summary>
    /// Whether a downloaded update may be installed as this process starts.
    /// </summary>
    /// <remarks>
    /// Only on a normal start. Installing restarts the app, and a start that is
    /// only answering an argument - or a hook an installer is running - is not
    /// the moment: it once turned <c>--version</c> into an install and a relaunch
    /// just to print a number. The update is still there for the next normal start.
    /// </remarks>
    public static bool MayApplyUpdates(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Length == 0;
    }

    private static string Help => new StringBuilder()
        .AppendLine("YappyNotes — desktop sticky notes.")
        .AppendLine()
        .AppendLine("Started with no arguments, it opens the manager and restores your notes.")
        .AppendLine()
        .AppendLine("  --version, -v   print the version and exit")
        .AppendLine("  --help, -h      print this and exit")
        .AppendLine("  --where         print the path of the notes database and exit")
        .AppendLine()
        .AppendLine("YAPPYNOTES_DATA_DIR overrides where the notes are kept. Without it the")
        .AppendLine("platform's convention is used, which honours XDG_DATA_HOME on Linux — some")
        .AppendLine("snap-packaged terminals point that into their own sandbox, so --where is the")
        .AppendLine("quickest way to see which database you are actually using.")
        .ToString();
}
