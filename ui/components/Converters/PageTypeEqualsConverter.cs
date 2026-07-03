using System.Globalization;
using Avalonia.Data.Converters;
using LimelightX.UI.Routing;

namespace LimelightX.UI.Components.Converters;

/// <summary>
/// Compares NavigationViewModel.CurrentPage against a PageType
/// ConverterParameter, used by Sidebar to drive the "active" style class /
/// aria-current="page" state (ui-styling-theming.md §4.3, ui-accessibility.md §12).
/// </summary>
public sealed class PageTypeEqualsConverter : IValueConverter
{
    public static readonly PageTypeEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PageType current && parameter is PageType target)
        {
            return current == target;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
