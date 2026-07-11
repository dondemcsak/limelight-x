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
[Trait("Category", "NativeTreeSitter")]
public class DiagnosticServiceTests
{
    /// <summary>bdd-ui-interactions.md §2.18, ui-intellisense-engine-spec.md §6.1: a MISSING "." node produces a specific message and SuggestedFix.</summary>
    [Theory]
    [InlineData("Summarize the article")]
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

    /// <summary>
    /// bdd-ui-interactions.md §2.7a, §2.18: the rule applies to every
    /// sentence in a multi-sentence document, not just the last/only one -
    /// GetDiagnostics walks the whole tree, so Tree-sitter's per-sentence
    /// GLR recovery (which inserts one clean MISSING "." at each malformed
    /// sentence boundary independently, confirmed by dumping the real parse
    /// tree) surfaces one diagnostic per missing period, regardless of
    /// whether sentences are newline- or space-separated, and regardless of
    /// how many are missing their period at once.
    /// </summary>
    [Theory]
    [InlineData("Load the article from \"a.txt\" Summarize it.", new[] { 29 })]
    [InlineData("Load the article from \"a.txt\"\nSummarize it", new[] { 29, 42 })]
    [InlineData("Load the article from \"a.txt\"\nSummarize it\nTranslate it to French", new[] { 29, 42, 65 })]
    public void GetDiagnostics_MissingPeriodOnNonLastSentence_ReturnsOneDiagnosticPerMissingPeriod(string text, int[] expectedStartBytes)
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();
        var root = parserHost.Parse(text);

        var diagnostics = diagnosticService.GetDiagnostics(root)
            .Where(d => d.Message == "Missing period at end of sentence.")
            .OrderBy(d => d.StartByte)
            .ToList();

        Assert.Equal(expectedStartBytes.Length, diagnostics.Count);
        for (var i = 0; i < expectedStartBytes.Length; i++)
        {
            Assert.Equal(expectedStartBytes[i], diagnostics[i].StartByte);
            Assert.Equal(".", diagnostics[i].SuggestedFix);
            Assert.Equal(diagnostics[i].StartByte, diagnostics[i].EndByte);
        }
    }

    /// <summary>bdd-ui-interactions.md §2.18, ui-intellisense-engine-spec.md §6.1: a MISSING "}}" node produces a specific message and SuggestedFix.</summary>
    [Fact]
    public void GetDiagnostics_MissingClosingBraceForExpressionHole_ReturnsBraceMessageWithSuggestedFix()
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();

        const string text = "Summarize the article using {{ prompt: \"abc\".";
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

        const string text = "Summarize the article.";
        var root = parserHost.Parse(text);

        Assert.Empty(diagnosticService.GetDiagnostics(root));
    }

    /// <summary>bdd-ui-interactions.md §2.28: a pronoun as the first sentence has no preceding sentence to refer to - flagged as an advisory diagnostic, distinct from ERROR/MISSING, with no SuggestedFix.</summary>
    [Fact]
    public void GetDiagnostics_PronounAsFirstSentence_ReturnsDanglingPronounDiagnosticWithNoSuggestedFix()
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();

        const string text = "Summarize it.";
        var root = parserHost.Parse(text);

        var diagnostic = Assert.Single(diagnosticService.GetDiagnostics(root));

        Assert.Equal("Pronoun has no preceding sentence to refer to.", diagnostic.Message);
        Assert.Null(diagnostic.SuggestedFix);
        Assert.Equal(text.IndexOf("it", StringComparison.Ordinal), diagnostic.StartByte);
    }

    /// <summary>bdd-ui-interactions.md §2.28: a pronoun with a genuine preceding sentence is not flagged.</summary>
    [Fact]
    public void GetDiagnostics_PronounWithPrecedingSentence_ReturnsNoDiagnostics()
    {
        using var parserHost = new ParserHost();
        var diagnosticService = new DiagnosticService();

        const string text = "Load the article from \"a.txt\".\nSummarize it.";
        var root = parserHost.Parse(text);

        Assert.Empty(diagnosticService.GetDiagnostics(root));
    }
}
