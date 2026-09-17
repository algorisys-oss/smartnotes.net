using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;
using Velopack.Locators;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

/// <summary>
/// Picks how this OS starts an app at login, for the copy that is running.
/// </summary>
/// <remarks>
/// Only a copy a Velopack installer put there has anywhere stable to start from.
/// Everything else - a build, an unpacked archive, <c>deploy-local.sh</c>'s folder -
/// gets a login item that is not available, and the system is never touched.
/// </remarks>
public static class LoginItems
{
    public static ILoginItem ForThisProcess()
    {
        if (!VelopackLocator.IsCurrentSet || VelopackLocator.Current.CurrentlyInstalledVersion is null)
        {
            return NotInstalled.Instance;
        }

        var locator = VelopackLocator.Current;

        if (OperatingSystem.IsWindows())
        {
            return new WindowsRunKey(InstalledWindowsExecutable(locator));
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new MacLaunchAgent(
                Path.Combine(home, "Library", "LaunchAgents"),
                Environment.ProcessPath is { } process ? MacLaunchAgent.BundleContaining(process) : null);
        }

        if (OperatingSystem.IsLinux())
        {
            // ApplicationData is XDG_CONFIG_HOME, or ~/.config without it - which
            // is where the autostart spec looks.
            return new XdgAutostartEntry(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart"),
                (locator as LinuxVelopackLocator)?.AppImagePath);
        }

        return NotInstalled.Instance;
    }

    /// <summary>
    /// The launcher Velopack leaves in the install's root, not the executable
    /// under <c>current\</c>: the launcher is the one that is still right after an
    /// update has swapped the folder beneath it.
    /// </summary>
    private static string? InstalledWindowsExecutable(IVelopackLocator locator)
    {
        var launcher = Path.Combine(locator.RootAppDir ?? string.Empty, "YappyNotes.App.exe");
        return File.Exists(launcher) ? launcher : Environment.ProcessPath;
    }

    private sealed class NotInstalled : ILoginItem
    {
        public static readonly NotInstalled Instance = new();

        public bool IsAvailable => false;

        public void Set(bool startAtLogin)
        {
        }
    }
}

/// <summary>
/// A freedesktop autostart entry, which GNOME, KDE, Xfce and the rest all read.
/// </summary>
/// <remarks>
/// Velopack installs on Linux as an AppImage, so the path to start is the
/// AppImage file - not this process's own path, which is inside a mount that
/// disappears when the app exits.
/// </remarks>
public sealed class XdgAutostartEntry(string autostartDirectory, string? appImagePath) : ILoginItem
{
    public const string FileName = "yappynotes.desktop";

    public bool IsAvailable => appImagePath is not null;

    public void Set(bool startAtLogin)
    {
        var entry = Path.Combine(autostartDirectory, FileName);

        if (!startAtLogin || appImagePath is null)
        {
            // File.Delete shrugs at a missing file but throws for a missing
            // folder, and a fresh account has no such folder at all.
            if (File.Exists(entry))
            {
                File.Delete(entry);
            }

            return;
        }

        Directory.CreateDirectory(autostartDirectory);
        File.WriteAllText(entry, new StringBuilder()
            .Append("[Desktop Entry]\n")
            .Append("Type=Application\n")
            .Append("Name=YappyNotes\n")
            .Append("Comment=Desktop sticky notes\n")
            // An AppImage is uninstalled by deleting it, and nothing of ours runs
            // when that happens. TryExec makes the desktop skip an entry whose
            // program is gone instead of trying it at every login.
            .Append("TryExec=").Append(EscapeString(appImagePath)).Append('\n')
            .Append("Exec=").Append(QuoteForExec(appImagePath)).Append('\n')
            .Append("X-GNOME-Autostart-enabled=true\n")
            .ToString());
    }

    /// <summary>
    /// Exec is parsed, not taken literally. Quoted, a space no longer splits
    /// arguments; inside the quotes <c>"</c>, <c>`</c>, <c>$</c> and <c>\</c> need a
    /// backslash; <c>%</c> starts a field code anywhere and is doubled; and the
    /// file format's own string escaping is applied on top, which doubles every
    /// backslash again. That last step is the one the spec calls out as easy to
    /// miss.
    /// </summary>
    private static string QuoteForExec(string path)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in path)
        {
            if (c is '"' or '`' or '$' or '\\')
            {
                quoted.Append('\\');
            }

            quoted.Append(c);
        }

        quoted.Append('"');
        return EscapeString(quoted.ToString()).Replace("%", "%%", StringComparison.Ordinal);
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal);
}

/// <summary>
/// A per-user LaunchAgent that opens the app bundle when the reader logs in.
/// </summary>
/// <remarks>
/// It opens the bundle through <c>open</c> rather than running the executable
/// inside it, so macOS starts it the way a double-click would. Not yet watched on
/// a Mac: the file is tested, launchd reading it is not.
/// </remarks>
public sealed class MacLaunchAgent(string launchAgentsDirectory, string? appBundlePath) : ILoginItem
{
    public const string FileName = "io.github.algorisys-oss.yappynotes.plist";

    private const string Label = "io.github.algorisys-oss.yappynotes";

    public bool IsAvailable => appBundlePath is not null;

    public void Set(bool startAtLogin)
    {
        var agent = Path.Combine(launchAgentsDirectory, FileName);

        if (!startAtLogin || appBundlePath is null)
        {
            // File.Delete shrugs at a missing file but throws for a missing
            // folder, and a fresh account has no such folder at all.
            if (File.Exists(agent))
            {
                File.Delete(agent);
            }

            return;
        }

        Directory.CreateDirectory(launchAgentsDirectory);

        // Built as XML rather than as a string, so a path with an ampersand in it
        // does not produce a plist launchd refuses to read.
        var plist = new XDocument(
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "Label"),
                    new XElement("string", Label),
                    new XElement("key", "ProgramArguments"),
                    new XElement("array",
                        new XElement("string", "/usr/bin/open"),
                        new XElement("string", "-a"),
                        new XElement("string", appBundlePath)),
                    new XElement("key", "RunAtLoad"),
                    new XElement("true"))));

        plist.Save(agent);
    }

    /// <summary>
    /// The <c>.app</c> a process is running from, or null when it is not in one.
    /// </summary>
    public static string? BundleContaining(string processPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processPath);

        // A bundle's executable is always at Name.app/Contents/MacOS/executable.
        var macOs = Path.GetDirectoryName(processPath);
        var contents = Path.GetDirectoryName(macOs);
        var bundle = Path.GetDirectoryName(contents);

        return Path.GetFileName(macOs) == "MacOS"
            && Path.GetFileName(contents) == "Contents"
            && bundle is not null
            && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? bundle
            : null;
    }
}

/// <summary>
/// The current user's <c>Run</c> key, which is what Task Manager's Startup tab
/// lists and lets somebody switch off without opening YappyNotes.
/// </summary>
public sealed class WindowsRunKey(string? executablePath) : ILoginItem
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "YappyNotes";

    public bool IsAvailable => executablePath is not null && OperatingSystem.IsWindows();

    public void Set(bool startAtLogin)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!startAtLogin || executablePath is null)
        {
            Remove();
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, CommandFor(executablePath));
    }

    /// <summary>
    /// Takes YappyNotes out of the Run key. Also what the uninstaller calls, so an
    /// uninstalled app does not leave Windows trying to start it at every login.
    /// </summary>
    public static void Remove()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Quoted, because unquoted a path under <c>C:\Users\A Reader</c> is read as
    /// <c>C:\Users\A</c> with an argument - or as <c>C:\Users\A.exe</c>, if one
    /// exists.
    /// </summary>
    public static string CommandFor(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\"";
    }
}
