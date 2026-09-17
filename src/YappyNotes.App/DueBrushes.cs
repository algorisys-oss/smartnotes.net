using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using YappyNotes.ViewModels;

namespace YappyNotes.App;

/// <summary>
/// What a due date looks like for how soon it is: red when overdue, amber today,
/// quiet otherwise.
/// </summary>
/// <remarks>
/// One set of brushes for the note and the manager's to-do view, so a date reads
/// the same wherever it is seen. Dark enough to read on every note colour, which
/// are all light paper; the red is the same family as a finished timer's.
/// </remarks>
public sealed class DueBrushes : IValueConverter
{
    public static DueBrushes Instance { get; } = new();

    private static readonly IBrush Overdue = new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
    private static readonly IBrush Today = new SolidColorBrush(Color.FromRgb(0x8A, 0x55, 0x00));
    private static readonly IBrush Later = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    public static IBrush For(DueState state) => state switch
    {
        DueState.Overdue => Overdue,
        DueState.Today => Today,
        _ => Later,
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => For(value is DueState state ? state : DueState.None);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("A colour does not map back to a due date.");
}
