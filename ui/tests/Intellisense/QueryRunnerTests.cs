using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests against the actual highlights/injections
/// .scm queries (spec/parsing/tree-sitter-runtime-build-guide.md).
/// </summary>
[Trait("Category", "NativeTreeSitter")]
public class QueryRunnerTests
{
    /// <summary>
    /// bdd-ui-interactions.md §2.5 (and its own regression coverage for the
    /// highlights.scm per-token-capture fix, finding #5): the "keyword"
    /// capture for "Load the" covers only that 8-byte literal token, not the
    /// whole sentence. Deliberately still exercised against the full
    /// sentence (including the part that currently ERRORs per
    /// ParserHostTests' tracked grammar defect) - this is exactly
    /// bdd-ui-interactions.md §2.7's "tokens it can still classify keep
    /// their normal highlight color" resilience, decoupled from whether the
    /// overall parse is error-free.
    /// </summary>
    [Fact]
    public void RunHighlights_LoadStatement_KeywordCaptureCoversOnlyTheLiteralTokenNotTheWholeSentence()
    {
        using var parserHost = new ParserHost();
        var queryRunner = new QueryRunner();

        var root = parserHost.Parse("Load the article from \"article.txt\".");
        var matches = queryRunner.RunHighlights(root);

        var keywordMatch = Assert.Single(matches, m => m.Capture == "keyword" && m.StartByte == 0 && m.EndByte == 8);
        Assert.Equal(0, keywordMatch.StartByte);
        Assert.Equal(8, keywordMatch.EndByte);
    }

    /// <summary>
    /// bdd-ui-interactions.md §2.6: the quoted string inside a prompt hole
    /// gets injected content styling. Was blocked by the tracked
    /// resource/keyword-boundary bug (spec/parsing/
    /// tree-sitter-runtime-build-guide.md §6): `resource`'s greedy,
    /// unbounded regex out-competed `pronoun` for the grammar's `input`
    /// choice and swallowed "it using {{ prompt: \"" whole, so no
    /// `prompt_hole` node - and therefore no injection match - ever existed.
    /// Fixed by lowering resource's word-token lexer precedence below every
    /// keyword literal (grammar.js's `_free_text_word`); "it" now correctly
    /// lexes as `pronoun` (precedence 0 beats resource-word's -1 at that
    /// shared starting position) and the sentence parses into a real
    /// `prompt_hole` with its inner `(string)` node captured by
    /// injections.scm.
    /// </summary>
    [Fact]
    public void RunInjections_PromptHole_InjectsOnlyTheQuotedStringContent()
    {
        using var parserHost = new ParserHost();
        var queryRunner = new QueryRunner();

        const string source = "Summarize it using {{ prompt: \"Summarize in 3 bullets.\" }}.";
        var promptStart = source.IndexOf("\"Summarize in 3 bullets.\"", StringComparison.Ordinal);
        var promptEnd = promptStart + "\"Summarize in 3 bullets.\"".Length;

        var root = parserHost.Parse(source);
        var matches = queryRunner.RunInjections(root).ToList();

        var match = Assert.Single(matches);
        Assert.Equal("injection.content", match.Capture);
        Assert.Equal(promptStart, match.StartByte);
        Assert.Equal(promptEnd, match.EndByte);
    }
}
