using System.Globalization;
using Avalonia.Data.Converters;

namespace LimelightX.UI.Components.Converters;

/// <summary>
/// Folds a tab's dirty/selected state into its accessible name
/// (ui-accessibility.md §12: dirty state must be exposed accessibly, not by
/// color/dot alone) - an IMultiValueConverter, not a plain object-binding
/// converter, so the name re-evaluates when IsDirty/IsActive change, not
/// just when the tab is first created.
/// </summary>
public sealed class TabAccessibleNameConverter : IMultiValueConverter
{
    public static readonly TabAccessibleNameConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var header = values.ElementAtOrDefault(0) as string ?? string.Empty;
        var isDirty = values.ElementAtOrDefault(1) is true;
        var isActive = values.ElementAtOrDefault(2) is true;

        List<string> parts = [header];
        if (isDirty)
        {
            parts.Add("unsaved changes");
        }

        if (isActive)
        {
            parts.Add("selected");
        }

        return string.Join(", ", parts);
    }
}
