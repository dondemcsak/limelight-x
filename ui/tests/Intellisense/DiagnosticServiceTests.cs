using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// DiagnosticService against the actual grammar/DLLs. The self-describing
/// missing-quote table entry (ui-intellisense-engine-spec.md §6.1) is not
/// covered here yet: this grammar's original string rule
/// (seq('"', repeat(/[^"]/), '"')) had no closing-quote anchor to recover
/// against once the closing quote was missing, so the parser fell back to
/// one blanket ERROR node rather than a clean MISSING '"' node for every
/// malformed input tried. Fixed in tree-sitter/grammar.js and
/// spec/parsing/grammer-js.md (string's content now excludes '\n', matching
/// _free_text_word's own boundary) - bounds the greedy repeat so recovery
/// has a concrete stopping point on any line but the last. Pending a DLL
/// rebuild (tree-sitter generate + the ARM64 MSVC build,
/// spec/parsing/tree-sitter-build-guide.md) and re-verification before a
/// GetDiagnostics_MissingClosingQuote_... test can be added here.
/// </summary>
[Trait("Category", "NativeArm64")]
public class DiagnosticServiceTests
{
    /// <summary>bdd-ui-interactions.md §2.18, ui-intellisense-engine-spec.md §6.1: a MISSING "." node produces a specific message and SuggestedFix.</summary>
    [Theory]
    [InlineData("Summarize it")]
    [InlineData("Load the article from \"a.txt\"")]
    public void GetDiagnostics_MissingPeriod_ReturnsPeriodMessageWithSuggestedFix(string text)
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();
        var root = parserHost.Parse(text);

        var diagnostic = Assert.Single(diagnosticService.GetDiagnostics(root));

        Assert.Equal("Missing period at end of sentence.", diagnostic.Message);
        Assert.Equal(".", diagnostic.SuggestedFix);
        Assert.Equal(diagnostic.StartByte, diagnostic.EndByte);
        Assert.Equal(text.Length, diagnostic.StartByte);
    }

    /// <summary>bdd-ui-interactions.md §2.18, ui-intellisense-engine-spec.md §6.1: a MISSING "}}" node produces a specific message and SuggestedFix.</summary>
    [Fact]
    public void GetDiagnostics_MissingClosingBraceForExpressionHole_ReturnsBraceMessageWithSuggestedFix()
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();

        const string text = "Summarize it using {{ prompt: \"abc\".";
        var root = parserHost.Parse(text);

        var diagnostic = Assert.Single(diagnosticService.GetDiagnostics(root));

        Assert.Equal("Missing closing '}}' for expression hole.", diagnostic.Message);
        Assert.Equal("}}", diagnostic.SuggestedFix);
        Assert.Equal(diagnostic.StartByte, diagnostic.EndByte);
    }

    /// <summary>bdd-ui-interactions.md §2.7-§2.8: an unrecognized verb produces a whole-span ERROR node with the unchanged generic message and no SuggestedFix.</summary>
    [Fact]
    public void GetDiagnostics_UnknownVerb_ReturnsUnexpectedTokenMessageWithNoSuggestedFix()
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();

        const string text = "Zorp the article.";
        var root = parserHost.Parse(text);

        var diagnostic = Assert.Single(diagnosticService.GetDiagnostics(root));

        Assert.Equal("Unexpected token.", diagnostic.Message);
        Assert.Null(diagnostic.SuggestedFix);
        Assert.Equal(0, diagnostic.StartByte);
        Assert.Equal(text.Length, diagnostic.EndByte);
    }

    /// <summary>bdd-ui-interactions.md §2.7-§2.8: well-formed text produces no diagnostics.</summary>
    [Fact]
    public void GetDiagnostics_ValidText_ReturnsNoDiagnostics()
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();

        const string text = "Summarize it.";
        var root = parserHost.Parse(text);

        Assert.Empty(diagnosticService.GetDiagnostics(root));
    }
}
