namespace YappyNotes.Core;

/// <summary>
/// Keeping a checklist going without typing its Markdown.
/// </summary>
/// <remarks>
/// A to-do is still <c>- [ ] text</c> in the note's content - these only write that
/// for the reader, so everything else that reads a note (search, export, the
/// formatted view) sees nothing new.
/// </remarks>
public static class TodoList
{
    /// <summary>
    /// What Enter does on a list line in the editor: start the next item, split the
    /// item at the caret, or - on an empty item - end the list.
    /// </summary>
    /// <returns>The edit, or null where Enter is just a line break.</returns>
    public static MarkdownEdit? ContinueOnEnter(string content, int caret)
    {
        ArgumentNullException.ThrowIfNull(content);

        var line = NoteMarkdown.Parse(content).FirstOrDefault(block =>
            caret >= block.SourceStart && caret <= block.SourceStart + block.SourceLength);

        if (line is not { Kind: MarkdownBlockKind.Task or MarkdownBlockKind.Bullet } || caret < line.TextStart)
        {
            return null;
        }

        var lineEnd = line.SourceStart + line.SourceLength;

        if (string.IsNullOrWhiteSpace(content[line.TextStart..lineEnd]))
        {
            // Enter on an empty item finishes the list, as in any editor: the marker
            // goes and the line is left for ordinary text.
            return new MarkdownEdit(content.Remove(line.SourceStart, line.SourceLength), line.SourceStart, line.SourceStart);
        }

        var before = caret;
        while (before > line.TextStart && content[before - 1] == ' ')
        {
            before--;
        }

        var after = caret;
        while (after < lineEnd && content[after] == ' ')
        {
            after++;
        }

        var inserted = LineBreakOf(content) + MarkerLike(content, line);
        var edited = string.Concat(content.AsSpan(0, before), inserted, content.AsSpan(after));
        var newCaret = before + inserted.Length;

        return new MarkdownEdit(edited, newCaret, newCaret);
    }

    /// <summary>
    /// Adds a to-do with this text: after the note's last to-do, at its indent, or
    /// as the start of a checklist at the end of a note that has none.
    /// </summary>
    /// <remarks>
    /// What was typed is the item's text rather than Markdown to interpret, so line
    /// breaks and runs of spaces are folded - a line break would split one item into
    /// an item and a stray line.
    /// </remarks>
    public static string Add(string content, string text)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(text);

        var tidy = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (tidy.Length == 0)
        {
            return content;
        }

        var lineBreak = LineBreakOf(content);
        var last = NoteMarkdown.Parse(content).LastOrDefault(block => block.Kind == MarkdownBlockKind.Task);

        if (last is not null)
        {
            var at = last.SourceStart + last.SourceLength;
            return content.Insert(at, lineBreak + MarkerLike(content, last) + tidy);
        }

        var item = "- [ ] " + tidy;

        if (content.Length == 0)
        {
            return item;
        }

        return content.EndsWith('\n') ? content + item : content + lineBreak + item;
    }

    /// <summary>
    /// A fresh marker for the next item in the same list: the same indent and the
    /// same bullet character, and an empty box however the line it follows is ticked.
    /// </summary>
    private static string MarkerLike(string content, MarkdownBlock line)
    {
        var indent = 0;
        while (content[line.SourceStart + indent] == ' ')
        {
            indent++;
        }

        var bullet = content[line.SourceStart + indent];
        var box = line.Kind == MarkdownBlockKind.Task ? "[ ] " : string.Empty;

        return $"{new string(' ', indent)}{bullet} {box}";
    }

    /// <summary>The note's own line ending, so a note written on Windows stays consistent.</summary>
    private static string LineBreakOf(string content) => content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
