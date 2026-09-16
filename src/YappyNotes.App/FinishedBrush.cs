using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YappyNotes.App;

/// <summary>
/// Colours the count once a countdown has run out.
/// </summary>
/// <remarks>
/// The display stops at 0:00 rather than counting into negative numbers, so
/// without this there is nothing separating "your break is over" from "your
/// break has not started". This is that difference.
/// </remarks>
public sealed class FinishedBrush : IValueConverter
{
    public static FinishedBrush Instance { get; } = new();

    private static readonly SolidColorBrush Finished = new(Color.FromRgb(0xB0, 0x1C, 0x1C));
    private static readonly SolidColorBrush Ordinary = new(Color.FromRgb(0x1A, 0x1A, 0x1A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Finished : Ordinary;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("A brush does not map back to a finished flag.");
}
