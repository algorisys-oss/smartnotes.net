using YappyNotes.Core;

namespace YappyNotes.Core.Tests;

/// <summary>How far through a checklist a note is, and clearing what is done.</summary>
public class TodoProgressTests
{
    [Fact]
    public void Progress_OfAChecklist_CountsTheTickedAndAllItems()
    {
        var progress = TodoList.Progress("# Shop\n- [x] bread\n- [ ] milk\n  - [X] oat\n- plain bullet");

        Assert.Equal(new TodoProgress(Done: 2, Total: 3), progress);
    }

    [Fact]
    public void Progress_OfANoteWithoutTodos_IsNothingOfNothing()
    {
        Assert.Equal(new TodoProgress(0, 0), TodoList.Progress("back soon"));
    }

    [Fact]
    public void ClearCompleted_RemovesEachTickedItemsLine()
    {
        Assert.Equal("# Shop\n- [ ] milk\nthanks", TodoList.ClearCompleted("# Shop\n- [x] bread\n- [ ] milk\n- [x] tea\nthanks"));
    }

    [Fact]
    public void ClearCompleted_OnTheLastLine_LeavesNoTrailingLineBreak()
    {
        Assert.Equal("- [ ] milk", TodoList.ClearCompleted("- [ ] milk\n- [x] bread"));
    }

    [Fact]
    public void ClearCompleted_WithEverythingDone_LeavesTheRestOfTheNote()
    {
        Assert.Equal("# Shop", TodoList.ClearCompleted("# Shop\n- [x] bread\n- [x] milk"));
    }

    /// <summary>
    /// A ticked item with an unticked one under it is a heading for work still to
    /// do. Removing it would leave the child hanging under whatever came before.
    /// </summary>
    [Fact]
    public void ClearCompleted_OnATickedItemWithUntickedItemsUnderIt_KeepsIt()
    {
        const string content = "- [x] groceries\n  - [ ] milk\n  - [x] bread";

        Assert.Equal("- [x] groceries\n  - [ ] milk", TodoList.ClearCompleted(content));
    }

    [Fact]
    public void ClearCompleted_OnATickedItemWhoseItemsAreAllTicked_RemovesThemAll()
    {
        Assert.Equal("- [ ] tea", TodoList.ClearCompleted("- [x] groceries\n  - [x] milk\n- [ ] tea"));
    }

    [Fact]
    public void ClearCompleted_OnWindowsLineEndings_KeepsThem()
    {
        Assert.Equal("- [ ] milk\r\nthanks", TodoList.ClearCompleted("- [x] bread\r\n- [ ] milk\r\nthanks"));
    }

    [Fact]
    public void ClearCompleted_WithNothingTicked_ChangesNothing()
    {
        Assert.Equal("- [ ] milk\n- [ ] bread", TodoList.ClearCompleted("- [ ] milk\n- [ ] bread"));
    }
}
