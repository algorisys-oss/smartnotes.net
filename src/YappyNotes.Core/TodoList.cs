namespace YappyNotes.Core;

/// <summary>How many of a note's to-dos are ticked, out of how many there are.</summary>
public sealed record TodoProgress(int Done, int Total);

/// <summary>A to-do read out of a note: where its box is, its words, its indent and its due date.</summary>
public sealed record TodoItem(int CheckMarkIndex, string Text, int Level, DateOnly? Due = null);

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
    /// The note's unticked to-dos, as plain words - for a list somewhere other than
    /// the note, where the Markdown around them would only be noise.
    /// </summary>
    public static IReadOnlyList<TodoItem> OpenItems(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return [.. NoteMarkdown.Parse(content)
            .Where(block => block is { Kind: MarkdownBlockKind.Task, IsDone: false })
            .Select(ItemOf)];
    }

    /// <summary>
    /// Ticks this item, if it is still in the note unticked where it was found.
    /// </summary>
    /// <remarks>
    /// The item comes from a list that can be older than the note. If the note has
    /// been edited since, a different item can sit where this one's box was, and
    /// ticking that one instead would be quietly wrong - so the text has to match,
    /// not just the position.
    /// </remarks>
    public static string Tick(string content, TodoItem item)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(item);

        var stillThere = NoteMarkdown.Parse(content).Any(block =>
            block is { Kind: MarkdownBlockKind.Task, IsDone: false }
            && block.CheckMarkIndex == item.CheckMarkIndex
            && ItemOf(block).Text == item.Text);

        return stillThere ? NoteMarkdown.ToggleTask(content, item.CheckMarkIndex) : content;
    }

    /// <summary>
    /// A to-do's words without its due date - the date is carried separately, and a
    /// list elsewhere shows it its own way - with the spaces it leaves folded.
    /// </summary>
    private static TodoItem ItemOf(MarkdownBlock block)
    {
        var words = string.Concat(block.Runs.Where(run => run.Due is null).Select(run => run.Text));
        var due = block.Runs.FirstOrDefault(run => run.Due is not null)?.Due;

        return new(
            block.CheckMarkIndex,
            string.Join(' ', words.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            block.Level,
            due);
    }

    /// <summary>How many of the note's to-dos are ticked, out of how many.</summary>
    public static TodoProgress Progress(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var todos = NoteMarkdown.Parse(content).Where(block => block.Kind == MarkdownBlockKind.Task).ToList();
        return new TodoProgress(todos.Count(todo => todo.IsDone), todos.Count);
    }

    /// <summary>
    /// Removes the line of every ticked to-do - except one with an unticked to-do
    /// under it, which is still a heading for work to do and would otherwise leave
    /// that item hanging under whatever came before.
    /// </summary>
    public static string ClearCompleted(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var blocks = NoteMarkdown.Parse(content);
        var kept = blocks
            .Where((block, i) => block is not { Kind: MarkdownBlockKind.Task, IsDone: true } || HasOpenTodoUnder(blocks, i))
            .ToList();

        if (kept.Count == blocks.Count)
        {
            return content;
        }

        // Rebuilt from the lines that stay rather than cut out of the text: each cut
        // moves every line after it, and a line removed last has to take the line
        // break before it instead of its own.
        return string.Join(LineBreakOf(content), kept.Select(block => content.Substring(block.SourceStart, block.SourceLength)));
    }

    /// <summary>
    /// Whether an unticked to-do sits under this item: among the list lines straight
    /// after it that are indented deeper.
    /// </summary>
    private static bool HasOpenTodoUnder(IReadOnlyList<MarkdownBlock> blocks, int index)
    {
        var level = blocks[index].Level;

        for (var j = index + 1; j < blocks.Count; j++)
        {
            if (blocks[j] is not { Kind: MarkdownBlockKind.Task or MarkdownBlockKind.Bullet } child || child.Level <= level)
            {
                return false;
            }

            if (child is { Kind: MarkdownBlockKind.Task, IsDone: false })
            {
                return true;
            }
        }

        return false;
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
