using LimelightX.UI.Intellisense;
using Xunit;
using static LimelightX.UI.Tests.Intellisense.TreeSitterTestHelpers;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// ParserHost against the actual tree-sitter-limelightx.dll and
/// tree-sitter-runtime.dll (spec/parsing/tree-sitter-runtime-build-guide.md).
/// </summary>
[Trait("Category", "NativeArm64")]
public class ParserHostTests
{
    /// <summary>bdd-ui-interactions.md §2.7: an incomplete sentence produces at least one ERROR/MISSING descendant, while a token it can still classify (the leading "Load the" keyword) remains its own, correctly-spanned node.</summary>
    [Fact]
    public void Parse_IncompleteSentence_ProducesErrorNodeWithLeadingKeywordStillClassified()
    {
        using var parserHost = new ParserHost();

        var root = parserHost.Parse("Load the article from");

        Assert.True(HasErrorDescendant(root), "expected at least one ERROR/MISSING descendant for incomplete input");

        var keyword = FindDescendant(root, n => NodeType(n) == "Load the");
        Assert.NotNull(keyword);
        Assert.Equal(0, (int)NativeMethods.ts_node_start_byte(keyword!.Value));
        Assert.Equal(8, (int)NativeMethods.ts_node_end_byte(keyword.Value));
    }

    /// <summary>
    /// A valid, complete sentence with no free-text-into-keyword ambiguity
    /// produces no ERROR/MISSING nodes at all - proves ParserHost/the
    /// runtime+grammar DLL pairing itself is sound. Deliberately does NOT
    /// use "Load the article from &quot;article.txt&quot;." (the example used
    /// throughout the specs) - see
    /// Parse_LoadStatementWithQuotedPathContainingPeriod_CurrentlyProducesErrorNode
    /// below for that one, and why it's still red.
    /// </summary>
    [Fact]
    public void Parse_SimpleCompleteSentence_ProducesNoErrorNodes()
    {
        using var parserHost = new ParserHost();

        var root = parserHost.Parse("Summarize it.");

        Assert.False(HasErrorDescendant(root), "expected no ERROR/MISSING descendants for a complete, valid sentence");
    }

    /// <summary>
    /// Was a known, tracked grammar defect (spec/parsing/
    /// tree-sitter-runtime-build-guide.md §6): grammar.js's `resource` rule
    /// had no keyword-boundary guard (unlike the PEG spec's `!KeywordWord`),
    /// so it greedily consumed through the following "from" keyword and the
    /// opening quote, only stopping at the period *inside* "article.txt" -
    /// not the sentence-terminating one. Fixed by tokenizing
    /// resource/target/format_target/language word-by-word with lower lexer
    /// precedence than every keyword literal (see grammar.js's
    /// `_free_text_word` and the "Fix Applied" note in the build guide
    /// above) - `resource` now correctly spans just "article", and the
    /// canonical example produces a clean `load_stmt` with no ERROR/MISSING
    /// descendants at all.
    /// </summary>
    [Fact]
    public void Parse_LoadStatementWithQuotedPathContainingPeriod_ProducesCleanLoadStmt()
    {
        using var parserHost = new ParserHost();

        var root = parserHost.Parse("Load the article from \"article.txt\".");

        Assert.False(HasErrorDescendant(root), "expected no ERROR/MISSING descendants now that the resource keyword-boundary bug is fixed");

        var resource = FindDescendant(root, n => NodeType(n) == "resource");
        Assert.NotNull(resource);
        Assert.Equal(9, (int)NativeMethods.ts_node_start_byte(resource!.Value));
        Assert.Equal(16, (int)NativeMethods.ts_node_end_byte(resource.Value));
    }
}
