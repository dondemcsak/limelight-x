using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// StructuralSelectionService against the actual grammar/DLLs.
/// </summary>
[Trait("Category", "NativeArm64")]
public class StructuralSelectionServiceTests
{
    /// <summary>
    /// bdd-ui-interactions.md §2.10: repeated invocation grows the selection
    /// child-to-parent, one grammar-meaningful step per call, up to the
    /// enclosing sentence - using the BDD scenario's own example (cursor
    /// inside a quoted string).
    /// </summary>
    [Fact]
    public void ExpandSelection_RepeatedlyInvokedInsideQuotedString_GrowsStringThenLoadStmtThenSentence()
    {
        using var parserHost = new ParserHost();
        var service = new StructuralSelectionService();

        const string text = "Load the article from \"a.txt\".";
        var root = parserHost.Parse(text);

        // Cursor inside "a.txt" (a zero-width point, not a selection yet).
        var insideQuote = text.IndexOf("a.txt", StringComparison.Ordinal) + 1;
        var step1 = service.ExpandSelection(root, insideQuote, insideQuote);

        var stringStart = text.IndexOf('"');
        var stringEnd = text.IndexOf('"', stringStart + 1) + 1;
        Assert.Equal((stringStart, stringEnd), step1);

        var step2 = service.ExpandSelection(root, step1.Start, step1.End);
        Assert.Equal((0, text.Length), step2);

        // Already at the outermost node - expanding again is a no-op.
        var step3 = service.ExpandSelection(root, step2.Start, step2.End);
        Assert.Equal(step2, step3);
    }

    /// <summary>
    /// Several grammar rules wrap a child with an identical byte span (e.g.
    /// "it" -> pronoun -> input in "Summarize it."). Expansion must collapse
    /// these into a single step rather than getting stuck re-selecting the
    /// same span forever.
    /// </summary>
    [Fact]
    public void ExpandSelection_AtNodeWithIdenticalSpanAncestors_SkipsToTheFirstStrictlyLargerOne()
    {
        using var parserHost = new ParserHost();
        var service = new StructuralSelectionService();

        const string text = "Summarize it.";
        var root = parserHost.Parse(text);
        var pronounStart = text.IndexOf("it", StringComparison.Ordinal);
        var pronounEnd = pronounStart + 2;

        var expanded = service.ExpandSelection(root, pronounStart, pronounEnd);

        Assert.Equal((0, text.Length), expanded);
    }
}
