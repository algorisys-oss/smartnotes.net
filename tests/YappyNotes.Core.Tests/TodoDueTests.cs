using YappyNotes.Core;

namespace YappyNotes.Core.Tests;

/// <summary>
/// A to-do's due date: written as <c>@2026-09-20</c> in its text, and typed as
/// <c>@tomorrow</c> or <c>@fri</c> when added.
/// </summary>
public class TodoDueTests
{
    /// <summary>A Thursday.</summary>
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static MarkdownRun? DueRun(string line)
        => NoteMarkdown.Parse(line)[0].Runs.SingleOrDefault(run => run.Due is not null);

    [Fact]
    public void Parse_ADateInATodo_IsARunThatKnowsTheDate()
    {
        var run = DueRun("- [ ] call Sam @2026-09-20");

        Assert.NotNull(run);
        Assert.Equal("@2026-09-20", run.Text);
        Assert.Equal(new DateOnly(2026, 9, 20), run.Due);
    }

    /// <summary>A date in ordinary prose is just part of the sentence.</summary>
    [Fact]
    public void Parse_ADateOutsideATodo_IsNotADueDate()
    {
        Assert.Null(DueRun("released @2026-09-20"));
    }

    [Theory]
    [InlineData("- [ ] call Sam @2026-02-30")]
    [InlineData("- [ ] mail me@2026-09-20")]
    [InlineData("- [ ] ticket @2026-09-201")]
    public void Parse_SomethingOnlyShapedLikeADate_IsNotADueDate(string line)
    {
        Assert.Null(DueRun(line));
    }

    [Fact]
    public void Parse_ADueDate_StillPointsAtItsOwnTextInTheNote()
    {
        const string content = "# Week\n- [ ] **call** Sam @2026-09-20 please";

        var run = NoteMarkdown.Parse(content)[1].Runs.Single(r => r.Due is not null);

        Assert.Equal(run.Text, content.Substring(run.SourceStart, run.Text.Length));
    }

    [Fact]
    public void OpenItems_WithADueDate_CarryIt()
    {
        var item = Assert.Single(TodoList.OpenItems("- [ ] call Sam @2026-09-20"));

        Assert.Equal(new DateOnly(2026, 9, 20), item.Due);
    }

    [Fact]
    public void OpenItems_WithADueDate_LeaveItOutOfTheirWords()
    {
        var item = Assert.Single(TodoList.OpenItems("- [ ] call Sam @2026-09-20"));

        Assert.Equal("call Sam", item.Text);
    }

    [Theory]
    [InlineData("call Sam @today", "call Sam @2026-09-17")]
    [InlineData("call Sam @Tomorrow", "call Sam @2026-09-18")]
    [InlineData("@fri call Sam", "@2026-09-18 call Sam")]
    [InlineData("call Sam @monday", "call Sam @2026-09-21")]
    public void ResolveShorthand_TurnsAWordIntoTheDateItMeansToday(string typed, string stored)
    {
        Assert.Equal(stored, TodoDue.ResolveShorthand(typed, Today));
    }

    /// <summary>"On Thursday", said on a Thursday, means today - as it does out loud.</summary>
    [Fact]
    public void ResolveShorthand_TheWeekdayItIsToday_MeansToday()
    {
        Assert.Equal("stand-up @2026-09-17", TodoDue.ResolveShorthand("stand-up @thu", Today));
    }

    [Theory]
    [InlineData("email me@today.example")]
    [InlineData("tag @someone")]
    [InlineData("nothing to see")]
    public void ResolveShorthand_OnAnythingElse_ChangesNothing(string typed)
    {
        Assert.Equal(typed, TodoDue.ResolveShorthand(typed, Today));
    }

    [Fact]
    public void Add_WithShorthandResolvedFirst_StoresARealDate()
    {
        var added = TodoList.Add(string.Empty, TodoDue.ResolveShorthand("call Sam @tomorrow", Today));

        Assert.Equal("- [ ] call Sam @2026-09-18", added);
    }
}
