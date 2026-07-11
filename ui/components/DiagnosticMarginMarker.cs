using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;

namespace LimelightX.UI.Components;

/// <summary>
/// Left-gutter margin drawing a small red dot on every line containing at
/// least one EditorViewModel.LocalDiagnostics entry (bdd-ui-interactions.md
/// §2.16), matching ui-error-handling.md §10.3's "red margin marker" for
/// authoritative errors, applied here to advisory Tree-sitter diagnostics.
/// Segment offsets are supplied pre-converted to UTF-16 char offsets by
/// CnlEditor, same as LocalDiagnosticsRenderer.
/// </summary>
public sealed class DiagnosticMarginMarker(Color color) : AbstractMargin
{
    private const double MarginWidth = 8.0;
    private const double Radius = 2.5;

    private readonly IBrush _brush = new SolidColorBrush(color);

    public IReadOnlyList<(int Start, int End)> Diagnostics { get; set; } = [];

    protected override Size MeasureOverride(Size availableSize) => new(MarginWidth, 0);

    public override void Render(DrawingContext drawingContext)
    {
        if (Diagnostics.Count == 0 || TextView is not { VisualLinesValid: true } textView)
        {
            return;
        }

        foreach (var line in textView.VisualLines)
        {
            var lineStart = line.StartOffset;
            var lineEnd = lineStart + line.VisualLength;

            var hasDiagnostic = Diagnostics.Any(d => d.Start < lineEnd && Math.Max(d.End, d.Start + 1) > lineStart);
            if (!hasDiagnostic)
            {
                continue;
            }

            var y = line.VisualTop - textView.VerticalOffset + line.Height / 2;
            drawingContext.DrawEllipse(_brush, null, new Point(MarginWidth / 2, y), Radius, Radius);
        }
    }
}
