using YappyNotes.App;

namespace YappyNotes.App.Tests;

/// <summary>
/// The handful of arguments that answer without opening a window.
/// </summary>
/// <remarks>
/// These exist for the release workflow. Cross-publishing six runtime
/// identifiers from Linux shows the files were produced, not that the binary
/// starts on Windows or macOS — and a GUI app on a runner has no display to open
/// into. Answering an argument and exiting is how it proves it runs.
/// </remarks>
public class CommandLineTests
{
    [Fact]
    public void TryAnswer_WithNoArguments_LeavesTheAppToStartNormally()
    {
        Assert.False(CommandLine.TryAnswer([], out _, out _));
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void TryAnswer_AskedForTheVersion_PrintsItAndSucceeds(string argument)
    {
        Assert.True(CommandLine.TryAnswer([argument], out var output, out var exitCode));

        Assert.Equal(0, exitCode);
        Assert.Contains("YappyNotes", output, StringComparison.Ordinal);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void TryAnswer_AskedForHelp_SaysWhatTheArgumentsAre(string argument)
    {
        Assert.True(CommandLine.TryAnswer([argument], out var output, out var exitCode));

        Assert.Equal(0, exitCode);
        Assert.Contains("--version", output, StringComparison.Ordinal);
        Assert.Contains("YAPPYNOTES_DATA_DIR", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TryAnswer_GivenSomethingItDoesNotKnow_FailsRatherThanOpeningAWindow()
    {
        // Silently starting the app on a typo is how "--verison" becomes a
        // support question.
        Assert.True(CommandLine.TryAnswer(["--verison"], out var output, out var exitCode));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("--verison", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TryAnswer_AskedWhereTheNotesAre_SaysSo()
    {
        // The XDG_DATA_HOME surprise documented in the README: a snap-packaged
        // terminal points it into its own sandbox, and the notes look lost.
        Assert.True(CommandLine.TryAnswer(["--where"], out var output, out var exitCode));

        Assert.Equal(0, exitCode);
        Assert.Contains("notes.db", output, StringComparison.Ordinal);
    }
}
