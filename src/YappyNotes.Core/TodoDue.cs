using System.Text.RegularExpressions;

namespace YappyNotes.Core;

/// <summary>
/// Due dates typed the way people say them.
/// </summary>
/// <remarks>
/// A note stores a due date as <c>@2026-09-20</c>, because a note is text that
/// outlives the day it was written: "@tomorrow" kept as typed would be wrong
/// tomorrow and every day after. So the words are turned into the date they mean
/// at the moment an item is added, and never stored.
/// </remarks>
public static partial class TodoDue
{
    /// <summary>
    /// Replaces <c>@today</c>, <c>@tomorrow</c> and a weekday - <c>@fri</c>,
    /// <c>@friday</c> - with the date it means, as <c>@2026-09-18</c>.
    /// </summary>
    /// <remarks>
    /// A weekday is the next one on or after today, so "@thu" said on a Thursday is
    /// today, as it would be out loud.
    /// </remarks>
    public static string ResolveShorthand(string text, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Shorthand().Replace(text, match =>
        {
            var word = match.Groups["word"].Value.ToLowerInvariant();
            var date = word switch
            {
                "today" => today,
                "tomorrow" => today.AddDays(1),
                _ => NextOnOrAfter(today, DayOf(word)),
            };

            return "@" + date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        });
    }

    private static DateOnly NextOnOrAfter(DateOnly today, DayOfWeek day)
        => today.AddDays(((int)day - (int)today.DayOfWeek + 7) % 7);

    private static DayOfWeek DayOf(string word) => word[..3] switch
    {
        "mon" => DayOfWeek.Monday,
        "tue" => DayOfWeek.Tuesday,
        "wed" => DayOfWeek.Wednesday,
        "thu" => DayOfWeek.Thursday,
        "fri" => DayOfWeek.Friday,
        "sat" => DayOfWeek.Saturday,
        _ => DayOfWeek.Sunday,
    };

    // A whole word after the at sign, and an at sign that is not the middle of an
    // email address.
    [GeneratedRegex(
        @"(?<![\w.@])@(?<word>today|tomorrow|mon(day)?|tues?(day)?|wed(nesday)?|thu(rs?)?(day)?|fri(day)?|sat(urday)?|sun(day)?)(?![\w.@-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Shorthand();
}
