using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace LimelightX.UI.Components;

/// <summary>
/// Draws a red zig-zag squiggly underline beneath each EditorViewModel.
/// LocalDiagnostics span (bdd-ui-interactions.md §2.16) - advisory only,
/// distinct in data model (not in visual shape - see §2.7) from
/// ValidationOverlay's /explain-driven error list (which, per its own doc
/// comment, can't render in-text markers yet since the backend's AST spans
/// are still {0,0} placeholders). Segment offsets are supplied pre-converted
/// to UTF-16 char offsets by CnlEditor. A zero-width MISSING span
/// (Start == End) is widened to a minimum one-character-cell width so it's
/// still visible (§2.16's "AS MEASURED BY").
/// </summary>
public sealed class LocalDiagnosticsRenderer(Color color) : IBackgroundRenderer
{
    private const double Amplitude = 2.0;
    private const double Step = 4.0;

    private readonly IPen _pen = new Pen(new SolidColorBrush(color), 1.3);

    public IReadOnlyList<(int Start, int End)> Diagnostics { get; set; } = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Diagnostics.Count == 0 || !textView.VisualLinesValid)
        {
            return;
        }

        foreach (var (start, end) in Diagnostics)
        {
            var segmentEnd = Math.Max(end, start + 1);
            var segment = new SimpleSegment(start, segmentEnd - start);

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment, true))
            {
                drawingContext.DrawGeometry(null, _pen, BuildSquiggleGeometry(rect));
            }
        }
    }

    private static StreamGeometry BuildSquiggleGeometry(Rect rect)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var y = rect.Bottom;
            context.BeginFigure(new Point(rect.Left, y), false);

            var x = rect.Left;
            var up = true;
            while (x < rect.Right)
            {
                var nextX = Math.Min(x + Step, rect.Right);
                context.LineTo(new Point(nextX, up ? y - Amplitude : y + Amplitude));
                x = nextX;
                up = !up;
            }

            context.EndFigure(false);
        }

        return geometry;
    }
}
