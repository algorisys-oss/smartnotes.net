using System.Text.RegularExpressions;

namespace SmartNotes.Core;

/// <summary>Where a link sits in a note's text, and where it points.</summary>
public sealed record LinkSpan(int Start, int Length, Uri Uri);

/// <summary>
/// Finds the links in a note's text, and decides which of them may be opened.
/// </summary>
/// <remarks>
/// <para>
/// A note's content is plain text - a link needs no schema, no editor work and no
/// Markdown, which is why this arrives long before any of those.
/// </para>
/// <para>
/// <b>The allow-list is the point.</b> Opening a link hands a string to the
/// operating system's shell, note content is text a reader can paste from
/// anywhere, and a notes.db can be copied between machines. So only http, https
/// and mailto are ever recognised: <c>file:</c> would open anything on disk, and
/// unknown schemes belong to whatever else is installed, which is a well-trodden
/// path from "clicked a link in a document" to "ran a program". Anything else
/// stays ordinary text.
/// </para>
/// </remarks>
public static partial class LinkScanner
{
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    /// <summary>Trailing punctuation that ends a sentence rather than an address.</summary>
    private const string SentenceTail = ".,;:!?";

    public static IReadOnlyList<LinkSpan> Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = new List<LinkSpan>();

        foreach (var match in LinkPattern().Matches(text).Cast<Match>())
        {
            var (start, length) = Trim(text, match.Index, match.Length);
            var candidate = text.Substring(start, length);

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && IsAllowed(uri))
            {
                found.Add(new LinkSpan(start, length, uri));
            }
        }

        return found;
    }

    /// <summary>Whether this address may be handed to the shell.</summary>
    public static bool IsAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Drops punctuation that belongs to the sentence rather than the link, and
    /// closing brackets that were never opened inside it - so "(see
    /// https://example.com)" links to the right place while a Wikipedia URL
    /// ending in ")" keeps it.
    /// </summary>
    private static (int Start, int Length) Trim(string text, int start, int length)
    {
        while (length > 0)
        {
            var last = text[start + length - 1];

            if (SentenceTail.Contains(last, StringComparison.Ordinal))
            {
                length--;
                continue;
            }

            if (last is ')' or ']' or '}')
            {
                var open = last switch { ')' => '(', ']' => '[', _ => '{' };
                var inside = text.AsSpan(start, length);

                if (Count(inside, open) < Count(inside, last))
                {
                    length--;
                    continue;
                }
            }

            break;
        }

        return (start, length);
    }

    private static int Count(ReadOnlySpan<char> text, char value)
    {
        var total = 0;
        foreach (var character in text)
        {
            if (character == value)
            {
                total++;
            }
        }

        return total;
    }

    // Only the allow-listed schemes are matched at all, so an unsupported one is
    // never even a candidate.
    [GeneratedRegex(@"\b(?:https?|mailto):[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();
}
