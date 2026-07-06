using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;
using LimelightX.UI.ViewModels.Workspace;

namespace LimelightX.UI.Components.Converters;

/// <summary>File-type icon per node (ui-styling-theming.md §4.4: "file-tree folder chevrons, per-file-type icons").</summary>
public sealed class FileTreeIconConverter : IValueConverter
{
    public static readonly FileTreeIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileTreeNodeViewModel node)
        {
            return Symbol.Document;
        }

        if (node.IsDirectory)
        {
            return Symbol.Folder;
        }

        return node.FullPath.EndsWith(".llx", StringComparison.OrdinalIgnoreCase) ? Symbol.DocumentText : Symbol.Document;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
