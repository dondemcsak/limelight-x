using System.Globalization;
using Avalonia.Data.Converters;

namespace LimelightX.UI.Components.Converters;

/// <summary>Formats an IrOperation.Input (int?) as its "$N" reference form (ui-components.md §4.3).</summary>
public sealed class DollarReferenceConverter : IValueConverter
{
    public static readonly DollarReferenceConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int input ? $"${input}" : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
