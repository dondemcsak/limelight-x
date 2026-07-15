using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace LimelightX.UI.Components;

/// <summary>
/// Minimal, deterministic Markdown-to-Avalonia-controls renderer (ui-components.md
/// §4.4-4.5: headers, bold, code fences, tables, lists). Hand-rolled per the
/// resolved dependency question - no Markdown library is on CLAUDE.md §3.5's
/// approved list. Covers only the constructs the spec asks for, not full
/// CommonMark.
/// </summary>
public static class MarkdownRenderer
{
    public static Control Render(string markdown)
    {
        var root = new StackPanel { Spacing = 6 };
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var codeLines = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].StartsWith("```", StringComparison.Ordinal))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }

                root.Children.Add(RenderCodeBlock(string.Join('\n', codeLines)));
                continue;
            }

            if (TryGetHeaderLevel(line, out var level, out var headerText))
            {
                root.Children.Add(RenderHeader(headerText, level));
                continue;
            }

            if (IsTableRow(line) && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                var tableLines = new List<string> { line };
                i += 2;
                while (i < lines.Length && IsTableRow(lines[i]))
                {
                    tableLines.Add(lines[i]);
                    i++;
                }

                i--;
                root.Children.Add(RenderTable(tableLines));
                continue;
            }

            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal) || line.TrimStart().StartsWith("* ", StringComparison.Ordinal))
            {
                root.Children.Add(RenderListItem(line.TrimStart()[2..]));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            root.Children.Add(RenderParagraph(line));
        }

        return root;
    }

    private static bool TryGetHeaderLevel(string line, out int level, out string text)
    {
        level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
        {
            level++;
        }

        if (level > 0 && level < line.Length && line[level] == ' ')
        {
            text = line[(level + 1)..];
            return true;
        }

        level = 0;
        text = string.Empty;
        return false;
    }

    private static bool IsTableRow(string line) => line.Contains('|');

    private static bool IsTableSeparator(string line) =>
        line.Replace("|", string.Empty).Replace(":", string.Empty).Replace(" ", string.Empty).All(c => c == '-') &&
        line.Contains('-');

    private static TextBlock RenderHeader(string text, int level) => new()
    {
        Text = text,
        FontFamily = (FontFamily)Application.Current!.FindResource("InterFontFamily")!,
        FontWeight = FontWeight.SemiBold,
        FontSize = level switch
        {
            1 => 20,
            2 => 17,
            3 => 15,
            _ => 14,
        },
        Foreground = (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!,
    };

    private static Control RenderParagraph(string line)
    {
        var textBlock = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current!.FindResource("InterFontFamily")!,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!,
        };

        foreach (var (segment, isBold) in SplitBold(line))
        {
            textBlock.Inlines ??= [];
            textBlock.Inlines.Add(new Run(segment) { FontWeight = isBold ? FontWeight.Bold : FontWeight.Normal });
        }

        return textBlock;
    }

    private static IEnumerable<(string Text, bool Bold)> SplitBold(string line)
    {
        var i = 0;
        while (i < line.Length)
        {
            var boldStart = line.IndexOf("**", i, StringComparison.Ordinal);
            if (boldStart < 0)
            {
                yield return (line[i..], false);
                yield break;
            }

            if (boldStart > i)
            {
                yield return (line[i..boldStart], false);
            }

            var boldEnd = line.IndexOf("**", boldStart + 2, StringComparison.Ordinal);
            if (boldEnd < 0)
            {
                yield return (line[boldStart..], false);
                yield break;
            }

            yield return (line[(boldStart + 2)..boldEnd], true);
            i = boldEnd + 2;
        }
    }

    private static Control RenderListItem(string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = "•",
            Foreground = (IBrush)Application.Current!.FindResource("AccentPrimaryBrush")!,
        });
        panel.Children.Add(RenderParagraph(text));
        return panel;
    }

    private static Control RenderCodeBlock(string code) => new Border
    {
        Background = (IBrush)Application.Current!.FindResource("SurfaceBrush")!,
        BorderBrush = (IBrush)Application.Current!.FindResource("BorderBrush")!,
        BorderThickness = new Avalonia.Thickness(1),
        CornerRadius = new Avalonia.CornerRadius(6),
        Padding = new Avalonia.Thickness(8),
        Child = new TextBlock
        {
            Text = code,
            FontFamily = (FontFamily)Application.Current!.FindResource("JetBrainsMonoFontFamily")!,
            FontSize = 12,
            Foreground = (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!,
            TextWrapping = TextWrapping.Wrap,
        },
    };

    private static Control RenderTable(List<string> tableLines)
    {
        var rows = tableLines.Select(SplitTableRow).ToList();
        var columnCount = rows.Count > 0 ? rows[0].Count : 0;

        var grid = new Grid();
        for (var c = 0; c < columnCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var rowBackground = r % 2 == 1
                ? (IBrush)Application.Current!.FindResource("SurfaceHoverBrush")!
                : (IBrush)Application.Current!.FindResource("SurfaceBrush")!;

            for (var c = 0; c < rows[r].Count; c++)
            {
                var cell = new Border
                {
                    Background = rowBackground,
                    Padding = new Avalonia.Thickness(6, 4),
                    Child = new TextBlock
                    {
                        Text = rows[r][c],
                        FontFamily = (FontFamily)Application.Current!.FindResource("InterFontFamily")!,
                        FontSize = 12,
                        FontWeight = r == 0 ? FontWeight.SemiBold : FontWeight.Normal,
                        Foreground = (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!,
                        TextWrapping = TextWrapping.Wrap,
                    },
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    private static List<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }
}
