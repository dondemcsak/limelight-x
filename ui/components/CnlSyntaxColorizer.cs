using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace LimelightX.UI.Components;

/// <summary>
/// Bridges SyntaxHighlighter's deterministic tokenizer into AvaloniaEdit's
/// per-visual-line rendering pipeline. One instance per CnlEditor; brushes
/// are resolved once from the style dictionaries (SyntaxColors.axaml) by the
/// caller rather than looked up per line.
/// </summary>
public sealed class CnlSyntaxColorizer(IReadOnlyDictionary<TokenKind, IBrush> brushes) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);
        var lineStart = line.Offset;

        foreach (var token in SyntaxHighlighter.Tokenize(lineText))
        {
            if (token.Kind == TokenKind.Plain || !brushes.TryGetValue(token.Kind, out var brush))
            {
                continue;
            }

            ChangeLinePart(lineStart + token.Start, lineStart + token.End, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
            });
        }
    }
}
