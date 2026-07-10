using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Components;

/// <summary>
/// Bridges QueryRunner's Tree-sitter highlights/injections queries
/// (bdd-ui-interactions.md §2.5-§2.6) into AvaloniaEdit's per-visual-line
/// rendering pipeline, replacing the hand-coded SyntaxHighlighter.Tokenize.
/// One instance per CnlEditor, owning a private ParserHost/QueryRunner (not
/// shared with the tab's EditorViewModel, which has its own for
/// FoldRegions/LocalDiagnostics) so highlighting always reflects the exact
/// text about to be rendered, with the same zero-staleness guarantee
/// SyntaxHighlighter.Tokenize provided by recomputing on every call.
/// Reparses once per Colorize pass (not once per line, since AvaloniaEdit
/// calls ColorizeLine once per visible line per pass) - ParserHost.Parse is
/// always a full reparse, so caching within a pass avoids reparsing the
/// whole document per visible line.
/// </summary>
public sealed class CnlSyntaxColorizer(IReadOnlyDictionary<TokenKind, IBrush> brushes) : DocumentColorizingTransformer, IDisposable
{
    // highlights.scm/injections.scm capture names -> the same TokenKind
    // classes SyntaxHighlighter.Tokenize produced, so SyntaxColors.axaml's
    // brush mapping needs no changes (§2.5's "no user-visible change").
    private static readonly Dictionary<string, TokenKind> CaptureKinds = new()
    {
        ["keyword"] = TokenKind.Keyword,
        ["string"] = TokenKind.String,
        ["variable"] = TokenKind.Resource,
        ["variable.builtin"] = TokenKind.Pronoun,
        ["embedded"] = TokenKind.ExpressionHole,
    };

    private readonly ParserHost _parserHost = new();
    private readonly QueryRunner _queryRunner = new();

    private Utf8Text? _currentText;
    private IReadOnlyList<QueryMatch> _currentHighlights = [];
    private IReadOnlyList<QueryMatch> _currentInjections = [];

    protected override void Colorize(ITextRunConstructionContext context)
    {
        var text = context.Document.Text;
        var utf8Text = new Utf8Text(text);
        var root = _parserHost.Parse(text);

        _currentText = utf8Text;
        _currentHighlights = [.. _queryRunner.RunHighlights(root)];
        _currentInjections = [.. _queryRunner.RunInjections(root)];

        base.Colorize(context);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_currentText is not { } utf8Text)
        {
            return;
        }

        // Highlights first, then injections on top: injections.scm's
        // (string) capture is nested inside a prompt_hole span that
        // highlights.scm's @embedded also covers, and the inner string must
        // keep its own String color rather than the hole's ExpressionHole
        // color (§2.6).
        ApplyMatches(_currentHighlights, line, utf8Text, static m => CaptureKinds.GetValueOrDefault(m.Capture, TokenKind.Plain));
        ApplyMatches(_currentInjections, line, utf8Text, static _ => TokenKind.String);
    }

    private void ApplyMatches(IReadOnlyList<QueryMatch> matches, DocumentLine line, Utf8Text utf8Text, Func<QueryMatch, TokenKind> classify)
    {
        foreach (var match in matches)
        {
            var kind = classify(match);
            if (kind == TokenKind.Plain || !brushes.TryGetValue(kind, out var brush))
            {
                continue;
            }

            var start = utf8Text.ByteOffsetToCharOffset(match.StartByte);
            var end = utf8Text.ByteOffsetToCharOffset(match.EndByte);
            if (end <= line.Offset || start >= line.EndOffset)
            {
                continue;
            }

            var partStart = Math.Max(start, line.Offset);
            var partEnd = Math.Min(end, line.EndOffset);

            ChangeLinePart(partStart, partEnd, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
            });
        }
    }

    public void Dispose() => _parserHost.Dispose();
}
