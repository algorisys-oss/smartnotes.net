using YappyNotes.App;

namespace YappyNotes.App.Tests;

/// <summary>
/// The Linux autostart entry, against a temp folder standing in for
/// <c>~/.config/autostart</c>.
/// </summary>
public sealed class XdgAutostartEntryTests : IDisposable
{
    private const string AppImage = "/home/reader/Applications/YappyNotes.AppImage";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"yappynotes-autostart-{Guid.CreateVersion7()}");

    private string EntryFile => Path.Combine(_directory, XdgAutostartEntry.FileName);

    private XdgAutostartEntry Entry(string? appImage = AppImage) => new(_directory, appImage);

    [Fact]
    public void Set_On_WritesAnEntryThatStartsTheAppImage()
    {
        Entry().Set(true);

        Assert.Contains($"Exec=\"{AppImage}\"", File.ReadAllLines(EntryFile));
    }

    /// <summary>
    /// A fresh account has no autostart folder until something makes one.
    /// </summary>
    [Fact]
    public void Set_On_WithNoAutostartFolderYet_MakesIt()
    {
        Assert.False(Directory.Exists(_directory));

        Entry().Set(true);

        Assert.True(File.Exists(EntryFile));
    }

    /// <summary>
    /// An AppImage is a file somebody deletes to uninstall, and nothing runs when
    /// they do. TryExec makes the desktop skip an entry whose program is gone,
    /// rather than try to start it at every login.
    /// </summary>
    [Fact]
    public void Set_On_NamesTheAppImageAsWhatMustExist()
    {
        Entry().Set(true);

        Assert.Contains($"TryExec={AppImage}", File.ReadAllLines(EntryFile));
    }

    [Fact]
    public void Set_Off_RemovesTheEntry()
    {
        Entry().Set(true);

        Entry().Set(false);

        Assert.False(File.Exists(EntryFile));
    }

    [Fact]
    public void Set_Off_WithNoEntryThere_DoesNothing()
    {
        var exception = Record.Exception(() => Entry().Set(false));

        Assert.Null(exception);
    }

    /// <summary>
    /// Exec is parsed, not taken literally: a space splits arguments, <c>%</c>
    /// starts a field code, and inside quotes <c>$</c> and the quote itself need a
    /// backslash - which the file format's own escaping then doubles.
    /// </summary>
    [Theory]
    [InlineData("/home/a reader/YappyNotes.AppImage", "Exec=\"/home/a reader/YappyNotes.AppImage\"")]
    [InlineData("/home/reader/100%/YappyNotes.AppImage", "Exec=\"/home/reader/100%%/YappyNotes.AppImage\"")]
    [InlineData("/home/reader/$apps/YappyNotes.AppImage", "Exec=\"/home/reader/\\\\$apps/YappyNotes.AppImage\"")]
    public void Set_On_WithAPathTheDesktopWouldMisread_QuotesIt(string appImage, string expected)
    {
        Entry(appImage).Set(true);

        Assert.Contains(expected, File.ReadAllLines(EntryFile));
    }

    [Fact]
    public void IsAvailable_WhenNotRunFromAnAppImage_IsFalse()
    {
        Assert.False(Entry(appImage: null).IsAvailable);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

/// <summary>
/// The macOS LaunchAgent, against a temp folder standing in for
/// <c>~/Library/LaunchAgents</c>. Written and read on any OS; only launchd itself
/// needs a Mac.
/// </summary>
public sealed class MacLaunchAgentTests : IDisposable
{
    private const string Bundle = "/Applications/YappyNotes.app";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"yappynotes-launchagents-{Guid.CreateVersion7()}");

    private string AgentFile => Path.Combine(_directory, MacLaunchAgent.FileName);

    private MacLaunchAgent Agent(string? bundle = Bundle) => new(_directory, bundle);

    [Fact]
    public void Set_On_WritesAnAgentThatOpensTheAppAtLogin()
    {
        Agent().Set(true);

        var plist = File.ReadAllText(AgentFile);
        Assert.Contains($"<string>{Bundle}</string>", plist);
        Assert.Contains("<key>RunAtLoad</key>", plist);
    }

    [Fact]
    public void Set_On_WithAnAmpersandInThePath_WritesValidXml()
    {
        Agent("/Users/r&d/Applications/YappyNotes.app").Set(true);

        var plist = System.Xml.Linq.XDocument.Load(AgentFile);
        Assert.Contains(
            plist.Descendants("string"),
            element => element.Value == "/Users/r&d/Applications/YappyNotes.app");
    }

    [Fact]
    public void Set_Off_RemovesTheAgent()
    {
        Agent().Set(true);

        Agent().Set(false);

        Assert.False(File.Exists(AgentFile));
    }

    [Fact]
    public void Set_Off_WithNoLaunchAgentsFolder_DoesNothing()
    {
        var exception = Record.Exception(() => Agent().Set(false));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("/Applications/YappyNotes.app/Contents/MacOS/YappyNotes.App", "/Applications/YappyNotes.app")]
    [InlineData("/Users/reader/build/YappyNotes.App", null)]
    public void BundleContaining_AProcessPath_FindsTheAppItRunsFrom(string processPath, string? expected)
    {
        Assert.Equal(expected, MacLaunchAgent.BundleContaining(processPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

public class WindowsRunKeyTests
{
    [Fact]
    public void CommandFor_AnExecutable_QuotesItsPath()
    {
        // Unquoted, a path under "C:\Users\A Reader" is read as C:\Users\A with an
        // argument - or, worse, as C:\Users\A.exe if one exists.
        Assert.Equal(
            "\"C:\\Users\\A Reader\\AppData\\Local\\YappyNotes\\YappyNotes.App.exe\"",
            WindowsRunKey.CommandFor(@"C:\Users\A Reader\AppData\Local\YappyNotes\YappyNotes.App.exe"));
    }
}
