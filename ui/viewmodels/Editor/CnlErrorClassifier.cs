using System.Text.RegularExpressions;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Editor;

/// <summary>
/// Classifies an ERR_CNL_PARSE error into a display kind (ui-error-handling.md
/// §6.2). The wire protocol has exactly one code/category for every CNL parse
/// failure (api.md §10) - this is a purely client-side heuristic over the
/// error's Location and Message against the current source text, matched
/// against the `{{ prompt: "..." }}` expression-hole grammar (cnl-grammar.md §4).
/// </summary>
public static partial class CnlErrorClassifier
{
    [GeneratedRegex("""\{\{\s*prompt\s*:\s*"(?:[^"\\]|\\.)*"\s*\}\}""")]
    private static partial Regex HolePattern();

    public static CnlErrorKind Classify(string sourceText, ErrorLocation? location, string message)
    {
        var offset = location is null ? null : LineColumnToOffset(sourceText, location.Line, location.Column);
        if (offset is int o && IsInsideHole(sourceText, o))
        {
            return CnlErrorKind.Hole;
        }

        if (message.Contains("hole", StringComparison.OrdinalIgnoreCase))
        {
            return CnlErrorKind.Hole;
        }

        if (message.Contains("unexpected", StringComparison.OrdinalIgnoreCase)
            || message.Contains("expected", StringComparison.OrdinalIgnoreCase))
        {
            return CnlErrorKind.Grammar;
        }

        return CnlErrorKind.Parser;
    }

    private static bool IsInsideHole(string sourceText, int offset)
    {
        foreach (Match match in HolePattern().Matches(sourceText))
        {
            if (offset >= match.Index && offset <= match.Index + match.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>1-based line/column (ui-data-contracts.md §3) to a 0-based character offset.</summary>
    private static int? LineColumnToOffset(string sourceText, int line, int column)
    {
        if (line <= 0)
        {
            return null;
        }

        var currentLine = 1;
        var offset = 0;
        while (currentLine < line)
        {
            var newlineIndex = sourceText.IndexOf('\n', offset);
            if (newlineIndex < 0)
            {
                return null;
            }

            offset = newlineIndex + 1;
            currentLine++;
        }

        return offset + Math.Max(0, column - 1);
    }
}
