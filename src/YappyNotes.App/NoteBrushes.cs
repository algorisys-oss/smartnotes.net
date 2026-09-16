using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using YappyNotes.Core;

namespace YappyNotes.App;

/// <summary>
/// What a <see cref="NoteColor"/> actually looks like.
/// </summary>
/// <remarks>
/// The mapping lives here rather than in Core, which has no opinion about
/// colour - it stores a name. Paper shades rather than saturated ones: a note is
/// something to read black text on for an hour.
/// </remarks>
public sealed class NoteBrushes : IValueConverter
{
    public static NoteBrushes Paper { get; } = new();

    public static NoteBrushes Edge { get; } = new() { _darker = true };

    private static readonly Dictionary<NoteColor, Color> Papers = new()
    {
        [NoteColor.Yellow] = Color.FromRgb(0xFF, 0xF3, 0x9A),
        [NoteColor.Green] = Color.FromRgb(0xC9, 0xEF, 0xB2),
        [NoteColor.Blue] = Color.FromRgb(0xB6, 0xDC, 0xF7),
        [NoteColor.Pink] = Color.FromRgb(0xFB, 0xC6, 0xDB),
        [NoteColor.Purple] = Color.FromRgb(0xDA, 0xC3, 0xF0),
        [NoteColor.Grey] = Color.FromRgb(0xE3, 0xE3, 0xE3),
    };

    private bool _darker;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var paper = value is NoteColor color && Papers.TryGetValue(color, out var found)
            ? found
            : Papers[NoteColor.Yellow];

        return new SolidColorBrush(_darker ? Darken(paper) : paper);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("A brush does not map back to a note colour.");

    // The header strip, so it reads as part of the same piece of paper.
    private static Color Darken(Color color) => Color.FromRgb(
        (byte)(color.R * 0.88),
        (byte)(color.G * 0.88),
        (byte)(color.B * 0.88));
}
