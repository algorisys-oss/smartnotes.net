using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace YappyNotes.App;

/// <summary>A list item's depth of indentation, as the margin that draws it.</summary>
public sealed class IndentMargin : IValueConverter
{
    public static IndentMargin Instance { get; } = new();

    private const double PerLevel = 18;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new Thickness(value is int level ? level * PerLevel : 0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("A margin does not map back to a depth.");
}
