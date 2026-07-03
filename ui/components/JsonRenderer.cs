using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace LimelightX.UI.Components;

/// <summary>
/// Minimal JSON pretty-printer + syntax colorizer (ui-components.md §4.5
/// "JSON syntax highlighting"). Hand-rolled alongside MarkdownRenderer for
/// the same reason - no rendering library is on the approved dependency list.
/// </summary>
public static class JsonRenderer
{
    public static Control Render(string json)
    {
        string pretty;
        try
        {
            using var document = JsonDocument.Parse(json);
            pretty = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            pretty = json;
        }

        var textBlock = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current!.FindResource("JetBrainsMonoFontFamily")!,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        var defaultBrush = (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!;
        var stringBrush = (IBrush)Application.Current!.FindResource("SyntaxStringBrush")!;
        var keywordBrush = (IBrush)Application.Current!.FindResource("SyntaxKeywordBrush")!;
        var resourceBrush = (IBrush)Application.Current!.FindResource("SyntaxResourceBrush")!;

        textBlock.Inlines ??= [];
        foreach (var token in Tokenize(pretty))
        {
            var brush = token.Kind switch
            {
                JsonTokenKind.String => stringBrush,
                JsonTokenKind.Keyword => keywordBrush,
                JsonTokenKind.Number => resourceBrush,
                _ => defaultBrush,
            };

            textBlock.Inlines.Add(new Run(token.Text) { Foreground = brush });
        }

        return textBlock;
    }

    private enum JsonTokenKind
    {
        Plain,
        String,
        Number,
        Keyword,
    }

    private readonly record struct JsonToken(string Text, JsonTokenKind Kind);

    private static IEnumerable<JsonToken> Tokenize(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            if (c == '"')
            {
                var end = i + 1;
                while (end < text.Length && text[end] != '"')
                {
                    if (text[end] == '\\' && end + 1 < text.Length)
                    {
                        end++;
                    }

                    end++;
                }

                end = Math.Min(end + 1, text.Length);
                yield return new JsonToken(text[i..end], JsonTokenKind.String);
                i = end;
            }
            else if (char.IsDigit(c) || (c == '-' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                var end = i;
                while (end < text.Length && (char.IsDigit(text[end]) || text[end] is '.' or '-' or '+' or 'e' or 'E'))
                {
                    end++;
                }

                yield return new JsonToken(text[i..end], JsonTokenKind.Number);
                i = end;
            }
            else if (char.IsLetter(c))
            {
                var end = i;
                while (end < text.Length && char.IsLetter(text[end]))
                {
                    end++;
                }

                var word = text[i..end];
                yield return new JsonToken(word, word is "true" or "false" or "null" ? JsonTokenKind.Keyword : JsonTokenKind.Plain);
                i = end;
            }
            else
            {
                yield return new JsonToken(c.ToString(), JsonTokenKind.Plain);
                i++;
            }
        }
    }
}
