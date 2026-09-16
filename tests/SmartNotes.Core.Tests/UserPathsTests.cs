using SmartNotes.Core;

namespace SmartNotes.Core.Tests;

public class UserPathsTests
{
    /// <summary>An environment with nothing in it.</summary>
    private static string? Nothing(string name) => null;

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables)
        => name => variables.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void Resolve_WithTheOverrideSet_UsesIt()
    {
        // scripts/dev-start.sh --sandbox sets this. If it stops being honoured,
        // a sandbox run silently opens the reader's real notes.
        var paths = UserPaths.Resolve(
            Environment((UserPaths.DirectoryOverrideVariable, "/tmp/somewhere-else")));

        Assert.Equal("/tmp/somewhere-else", paths.DataDirectory);
    }

    [Fact]
    public void Resolve_WithABlankOverride_FallsBackToThePlatformDefault()
    {
        var paths = UserPaths.Resolve(
            Environment((UserPaths.DirectoryOverrideVariable, "   ")));

        Assert.Equal(UserPaths.Resolve(Nothing).DataDirectory, paths.DataDirectory);
    }

    [Fact]
    public void Resolve_WithNoOverride_PutsTheDataSomewhereAbsoluteNamedForTheApp()
    {
        var paths = UserPaths.Resolve(Nothing);

        Assert.True(Path.IsPathRooted(paths.DataDirectory), paths.DataDirectory);
        Assert.Equal("SmartNotes", Path.GetFileName(paths.DataDirectory));
    }

    [Fact]
    public void DatabaseFile_IsAlwaysNotesDbInsideTheDataDirectory()
    {
        var paths = new UserPaths(Path.Combine("/tmp", "sandbox"));

        Assert.Equal(Path.Combine("/tmp", "sandbox", "notes.db"), paths.DatabaseFile);
    }

    [Fact]
    public void UserPaths_GivenABlankDirectory_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new UserPaths("  "));
    }

    [Fact]
    public void EnsureCreated_WhenTheDirectoryIsNotThere_MakesIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smartnotes-{Guid.CreateVersion7()}");
        try
        {
            var paths = new UserPaths(directory);
            Assert.False(Directory.Exists(directory));

            paths.EnsureCreated();

            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EnsureCreated_CalledTwice_DoesNotThrow()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smartnotes-{Guid.CreateVersion7()}");
        try
        {
            var paths = new UserPaths(directory);
            paths.EnsureCreated();
            paths.EnsureCreated();

            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_OnLinux_HonoursXdgDataHome()
    {
        if (!OperatingSystem.IsLinux()) return;

        var paths = UserPaths.Resolve(Environment(("XDG_DATA_HOME", "/tmp/xdg")));

        Assert.Equal(Path.Combine("/tmp", "xdg", "SmartNotes"), paths.DataDirectory);
    }

    /// <summary>
    /// The three platforms disagree about where a file like this goes, and the
    /// README promises one answer each. Only the running platform's promise can
    /// be checked here, which is why CI runs on more than one.
    /// </summary>
    [Fact]
    public void Resolve_WithNoOverride_FollowsThisPlatformsConvention()
    {
        var directory = UserPaths.Resolve(Nothing).DataDirectory;
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SmartNotes"),
                directory);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal(Path.Combine(home, "Library", "Application Support", "SmartNotes"), directory);
        }
        else
        {
            Assert.Equal(Path.Combine(home, ".local", "share", "SmartNotes"), directory);
        }
    }
}
