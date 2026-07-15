using System.Globalization;
using Avalonia.Data.Converters;

namespace LimelightX.UI.Components.Converters;

/// <summary>Two-way int&lt;-&gt;string conversion for TextField, used by SettingsPage's Port field.</summary>
public sealed class IntToStringConverter : IValueConverter
{
    public static readonly IntToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int intValue ? intValue.ToString(culture) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && int.TryParse(text, out var result) ? result : 0;
}
