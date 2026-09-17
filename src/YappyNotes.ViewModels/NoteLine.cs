using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>
/// One line of a note as it is drawn: a marker, then the line's runs.
/// </summary>
/// <remarks>
/// The marker is a character drawn rather than a control - a box for a checklist
/// item, a dot for a bullet - so that the whole line is one run of text that
/// wraps like one, and a click anywhere on it can be answered by counting
/// characters. Indentation is not in the marker; the view draws it as a margin so
/// a wrapped line keeps its indent.
/// </remarks>
public sealed record NoteLine(MarkdownBlock Block, string Marker)
{
    public const string ToDo = "☐ ";
    public const string Done = "☑ ";
    public const string Bullet = "• ";

    public static NoteLine For(MarkdownBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var marker = block.Kind switch
        {
            MarkdownBlockKind.Task => block.IsDone ? Done : ToDo,
            MarkdownBlockKind.Bullet => Bullet,
            _ => string.Empty,
        };

        return new NoteLine(block, marker);
    }

    /// <summary>Whether the character drawn at this index is the checklist box.</summary>
    public bool IsCheckBoxAt(int renderedIndex)
        => Block.Kind == MarkdownBlockKind.Task && renderedIndex >= 0 && renderedIndex < Marker.Length;

    /// <summary>The run drawn at this index, or null over the marker or past the end.</summary>
    public MarkdownRun? RunAt(int renderedIndex)
    {
        var offset = renderedIndex - Marker.Length;

        foreach (var run in Block.Runs)
        {
            if (offset >= 0 && offset < run.Text.Length)
            {
                return run;
            }

            offset -= run.Text.Length;
        }

        return null;
    }

    /// <summary>
    /// Where in the note's Markdown the character drawn at this index came from.
    /// </summary>
    /// <remarks>
    /// Exact within a run, because a run's text is an unbroken piece of the source.
    /// Over the marker it is the start of the line's text, and past the end it is
    /// the end of the line - which is where a click in either place would expect
    /// the caret.
    /// </remarks>
    public int SourceIndexAt(int renderedIndex)
    {
        var lineEnd = Block.SourceStart + Block.SourceLength;

        if (renderedIndex < Marker.Length)
        {
            return Block.Runs.Count > 0 ? Block.Runs[0].SourceStart : lineEnd;
        }

        var offset = renderedIndex - Marker.Length;

        foreach (var run in Block.Runs)
        {
            if (offset < run.Text.Length)
            {
                return run.SourceStart + offset;
            }

            offset -= run.Text.Length;
        }

        return lineEnd;
    }
}
