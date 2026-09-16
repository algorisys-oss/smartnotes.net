using SmartNotes.Core;

namespace SmartNotes.Core.Tests;

public class LinkScannerTests
{
    private static IReadOnlyList<string> UrlsIn(string text)
        => [.. LinkScanner.Scan(text).Select(link => link.Uri.ToString())];

    [Fact]
    public void Scan_OnTextWithNoLink_FindsNothing()
    {
        Assert.Empty(LinkScanner.Scan("just a note about milk"));
    }

    [Fact]
    public void Scan_FindsAnHttpsUrl()
    {
        Assert.Equal(["https://twitch.tv/rajesh"], UrlsIn("back in 5 https://twitch.tv/rajesh"));
    }

    [Fact]
    public void Scan_FindsSeveralLinksInOneNote()
    {
        var text = "http://example.com and https://example.org/docs";

        Assert.Equal(["http://example.com/", "https://example.org/docs"], UrlsIn(text));
    }

    [Fact]
    public void Scan_FindsAMailtoLink()
    {
        Assert.Equal(["mailto:someone@example.com"], UrlsIn("ask mailto:someone@example.com"));
    }

    [Fact]
    public void Scan_ReportsWhereInTheTextEachLinkIs()
    {
        // The renderer needs the span, not just the address.
        var text = "see https://example.com now";

        var link = Assert.Single(LinkScanner.Scan(text));

        Assert.Equal("https://example.com", text.Substring(link.Start, link.Length));
    }

    /// <summary>
    /// A sentence ending in a link should not swallow the full stop into the
    /// address, which is the difference between a link that works and one that
    /// 404s.
    /// </summary>
    [Theory]
    [InlineData("see https://example.com.", "https://example.com")]
    [InlineData("see https://example.com,", "https://example.com")]
    [InlineData("(see https://example.com)", "https://example.com")]
    [InlineData("see https://example.com!", "https://example.com")]
    public void Scan_WithPunctuationAfterALink_LeavesItOutOfTheAddress(string text, string expected)
    {
        var link = Assert.Single(LinkScanner.Scan(text));

        Assert.Equal(expected, text.Substring(link.Start, link.Length));
    }

    [Fact]
    public void Scan_KeepsAPathThatLegitimatelyEndsInABracket()
    {
        var text = "https://en.wikipedia.org/wiki/Note_(disambiguation)";

        Assert.Equal([text], UrlsIn(text));
    }

    /// <summary>
    /// Note content is text a reader can paste from anywhere, and a notes.db
    /// travels between machines. Anything not on the allow-list is left as
    /// ordinary text rather than becoming something clickable that hands a
    /// string to the OS shell.
    /// </summary>
    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("smb://192.168.0.1/share")]
    [InlineData("ms-msdt:/id")]
    public void Scan_OnASchemeNotOnTheAllowList_FindsNoLinkAtAll(string dangerous)
    {
        Assert.Empty(LinkScanner.Scan($"look at {dangerous} please"));
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("mailto:someone@example.com", true)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://example.com", false)]
    public void IsAllowed_SaysWhetherAUriMayBeHandedToTheShell(string uri, bool expected)
    {
        Assert.Equal(expected, LinkScanner.IsAllowed(new Uri(uri)));
    }

    [Fact]
    public void Scan_OnAnEmptyNote_FindsNothingRatherThanThrowing()
    {
        Assert.Empty(LinkScanner.Scan(string.Empty));
    }
}
