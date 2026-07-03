using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.Components;

/// <summary>Dispatches to MarkdownRenderer/JsonRenderer/plain text based on ResultContentType (ui-components.md §4.5-4.6).</summary>
public static class ContentRenderer
{
    public static Control Render(string text, ResultContentType contentType) => contentType switch
    {
        ResultContentType.Markdown => MarkdownRenderer.Render(text),
        ResultContentType.Json => JsonRenderer.Render(text),
        _ => new TextBlock
        {
            Text = text,
            FontFamily = (FontFamily)Application.Current!.FindResource("InterFontFamily")!,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!,
        },
    };
}
