using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed test - no fakes, exercises the concrete
/// FoldingService against the actual grammar/queries.
/// </summary>
[Trait("Category", "NativeArm64")]
public class FoldingServiceTests
{
    /// <summary>
    /// bdd-ui-interactions.md §2.9: exactly one fold region per top-level
    /// `sentence` CST node, each entry's [StartByte, EndByte) matching that
    /// sentence's own span. Deliberately uses two pronoun-only sentences
    /// ("Summarize it."/"Rewrite it.") rather than the canonical
    /// "Load the article from ..." example: that one hits the tracked
    /// resource/keyword-boundary grammar bug (spec/parsing/
    /// tree-sitter-runtime-build-guide.md §6) hard enough that the parser's
    /// error recovery drops the first sentence out of the CST entirely
    /// (confirmed empirically - it yields 1 fold, not 2), unlike
    /// ParserHostTests' single-sentence case where recovery stays local. See
    /// ParserHostTests for that dedicated bug-tracking coverage; this test
    /// stays scoped to what FoldingService is actually responsible for.
    /// </summary>
    [Fact]
    public void GetFolds_TwoSentenceDocument_ReturnsExactlyTwoFoldRegions()
    {
        using var parserHost = new ParserHost();
        var queryRunner = new QueryRunner();
        var foldingService = new FoldingService(queryRunner);

        const string source = "Summarize it.\nRewrite it.";
        var firstSentenceEnd = source.IndexOf('.') + 1;
        var secondSentenceStart = source.IndexOf("Rewrite", StringComparison.Ordinal);

        var root = parserHost.Parse(source);
        var folds = foldingService.GetFolds(root).ToList();

        Assert.Equal(2, folds.Count);
        Assert.Contains(folds, f => f.StartByte == 0 && f.EndByte == firstSentenceEnd);
        Assert.Contains(folds, f => f.StartByte == secondSentenceStart && f.EndByte == source.Length);
    }
}
