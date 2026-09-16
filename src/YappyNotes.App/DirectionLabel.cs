using System.Globalization;
using Avalonia.Data.Converters;

namespace YappyNotes.App;

/// <summary>
/// Names the direction button after what it currently is, not what pressing it
/// would do — a button reading "Count up" while the thing counts down is the
/// classic way to make a toggle unreadable.
/// </summary>
public sealed class DirectionLabel : IValueConverter
{
    public static DirectionLabel Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "↓ counting down" : "↑ counting up";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("The label does not map back to a direction.");
}
