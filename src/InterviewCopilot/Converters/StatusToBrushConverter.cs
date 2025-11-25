using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace InterviewCopilot.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x57, 0xD0, 0xFF)); // Primary
    private static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x00));
    private static readonly Brush Err = new SolidColorBrush(Color.FromRgb(0xFF, 0x56, 0x56));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value as string) ?? string.Empty;
        if (s.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)) return Err;
        if (s.Contains("Ready", StringComparison.OrdinalIgnoreCase) || s.Contains("Capturing", StringComparison.OrdinalIgnoreCase) || s.Contains("Listening", StringComparison.OrdinalIgnoreCase)) return Ok;
        if (s.Contains("Paused", StringComparison.OrdinalIgnoreCase) || s.Contains("Spooling", StringComparison.OrdinalIgnoreCase)) return Warn;
        return Muted;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

