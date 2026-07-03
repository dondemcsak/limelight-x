using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using LimelightX.UI.Services.Dto;

namespace LimelightX.UI.Components;

/// <summary>
/// Prompt blocks, syntax-highlighted Markdown, operation index labels
/// (ui-components.md §4.4). Trace-only. Uses a code-behind FuncDataTemplate
/// since rendering Markdown requires building controls imperatively
/// (MarkdownRenderer.Render), not a pure XAML DataTemplate.
/// </summary>
public partial class PromptPanel : UserControl
{
    public PromptPanel()
    {
        InitializeComponent();

        PromptsList.ItemTemplate = new FuncDataTemplate<PromptBlock>((block, _) =>
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
            content.Children.Add(MarkdownRenderer.Render(block.PromptText));

            card.Child = content;
            return card;
        });
    }
}
