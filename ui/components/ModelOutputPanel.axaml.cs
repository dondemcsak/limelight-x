using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using LimelightX.UI.Services.Dto;

namespace LimelightX.UI.Components;

/// <summary>
/// Syntax-highlighted Markdown/JSON model output rendering (ui-components.md
/// §4.5). Trace-only. Renders from ModelOutputBlock.RawText, not
/// Parsed.Markdown (confirmed always null server-side today).
/// </summary>
public partial class ModelOutputPanel : UserControl
{
    public ModelOutputPanel()
    {
        InitializeComponent();

        OutputsList.ItemTemplate = new FuncDataTemplate<ModelOutputBlock>((block, _) =>
        {
            var card = new Border
            {
                Classes = { "card" },
                Margin = new Thickness(0, 0, 0, 8),
                Padding = (Thickness)Application.Current!.FindResource("PaddingMediumThickness")!,
            };

            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(new TextBlock
            {
                Text = $"operation {block.OperationIndex}",
                Foreground = (IBrush)Application.Current!.FindResource("TextMutedBrush")!,
                FontFamily = (FontFamily)Application.Current!.FindResource("JetBrainsMonoFontFamily")!,
                FontSize = 11,
            });
            content.Children.Add(ContentRenderer.Render(block.RawText, MapContentType(block.ContentType)));
            content.Children.Add(new TextBlock
            {
                Text = $"{block.Metadata.TokenUsage} tokens · {block.Metadata.LatencyMs} ms",
                Foreground = (IBrush)Application.Current!.FindResource("TextMutedBrush")!,
                FontFamily = (FontFamily)Application.Current!.FindResource("InterFontFamily")!,
                FontSize = 11,
            });

            card.Child = content;
            return card;
        });
    }

    private static ViewModels.Inspectors.ResultContentType MapContentType(Services.Dto.ResultContentType wireType) => wireType switch
    {
        Services.Dto.ResultContentType.Markdown => ViewModels.Inspectors.ResultContentType.Markdown,
        Services.Dto.ResultContentType.Json => ViewModels.Inspectors.ResultContentType.Json,
        _ => ViewModels.Inspectors.ResultContentType.PlainText,
    };
}
