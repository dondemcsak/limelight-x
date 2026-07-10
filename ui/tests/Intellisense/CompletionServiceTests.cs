using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// CompletionService against the actual grammar/DLLs.
/// </summary>
[Trait("Category", "NativeArm64")]
public class CompletionServiceTests
{
    /// <summary>
    /// bdd-ui-interactions.md §2.12: at a position where LoadStmt's grammar
    /// allows only "from" next, completions must contain exactly that
    /// keyword and nothing else. Only reachable now that the tracked
    /// resource/keyword-boundary bug is fixed (spec/parsing/
    /// tree-sitter-runtime-build-guide.md §6) - CompletionService's
    /// trial-insertion strategy needs a real `resource`+`from` node pair to
    /// appear when "from" is spliced in, which the pre-fix grammar never
    /// produced for any candidate.
    /// </summary>
    [Fact]
    public void GetCompletions_AfterLoadStatementResource_SuggestsOnlyFromKeyword()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Load the article ";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions);
        Assert.Equal("from", item.Text);
    }

    /// <summary>
    /// bdd-ui-interactions.md §2.13: the cursor resolves inside a `resource`
    /// span (free-text noun phrase), so completions must stay empty rather
    /// than fabricate suggestions for content the grammar deliberately
    /// leaves unconstrained.
    /// </summary>
    [Fact]
    public void GetCompletions_InsideResourceSpan_ReturnsEmpty()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Load the article from \"a.txt\".";
        var cursorByte = text.IndexOf("article", StringComparison.Ordinal) + 3; // inside "article"
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, cursorByte);

        Assert.Empty(completions);
    }
}
