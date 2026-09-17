using YappyNotes.Core;

namespace YappyNotes.Core.Tests;

/// <summary>
/// Ticking a checklist item while the note is showing formatted text.
/// </summary>
public class NoteMarkdownTaskTests
{
    private static int CheckMarkOf(string content, int block = 0) => NoteMarkdown.Parse(content)[block].CheckMarkIndex;

    [Fact]
    public void ToggleTask_OnABoxStillToDo_TicksIt()
    {
        const string content = "- [ ] milk";

        Assert.Equal("- [x] milk", NoteMarkdown.ToggleTask(content, CheckMarkOf(content)));
    }

    [Theory]
    [InlineData("- [x] milk")]
    [InlineData("- [X] milk")]
    public void ToggleTask_OnATickedBox_ClearsIt(string content)
    {
        Assert.Equal("- [ ] milk", NoteMarkdown.ToggleTask(content, CheckMarkOf(content)));
    }

    [Fact]
    public void ToggleTask_OnOneBoxOfSeveral_LeavesTheRestOfTheNoteAlone()
    {
        const string content = "# Shopping\r\n- [ ] milk\r\n  - [x] bread\r\nthanks";

        var toggled = NoteMarkdown.ToggleTask(content, CheckMarkOf(content, block: 2));

        Assert.Equal("# Shopping\r\n- [ ] milk\r\n  - [ ] bread\r\nthanks", toggled);
    }

    /// <summary>
    /// The position comes from a parse of the note as it was drawn. If the text
    /// has changed since - typed in another way, restored from the archive - a
    /// stale position must not flip whatever character now sits there.
    /// </summary>
    [Theory]
    [InlineData("milk", 3)]
    [InlineData("- [ ] milk", 99)]
    [InlineData("- [ ] milk", -1)]
    [InlineData("a [b] c", 3)]
    public void ToggleTask_AtAPositionThatIsNotACheckBox_ChangesNothing(string content, int position)
    {
        Assert.Equal(content, NoteMarkdown.ToggleTask(content, position));
    }
}
