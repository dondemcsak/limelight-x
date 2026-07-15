using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;
using Xunit;
using static LimelightX.UI.Tests.Intellisense.TreeSitterTestHelpers;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// CompletionService against the actual grammar/DLLs.
/// </summary>
[Trait("Category", "NativeTreeSitter")]
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

    /// <summary>bdd-ui-interactions.md §2.30: with "Summariz" typed (one letter short of "Summarize"), completion suggests exactly "Summarize", carrying the 8-char already-typed prefix so the caller knows what to replace.</summary>
    [Fact]
    public void GetCompletions_PartialVerbPrefix_ReturnsOnlyMatchingVerb()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Summariz";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions);
        Assert.Equal("Summarize", item.Text);
        Assert.Equal(8, item.PrefixLength);
    }

    /// <summary>bdd-ui-interactions.md §2.30: a typed prefix that diverges from every grammar-valid candidate yields no completions.</summary>
    [Fact]
    public void GetCompletions_PrefixMatchesNoCandidate_ReturnsEmpty()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Summarizzz";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length);

        Assert.Empty(completions);
    }

    /// <summary>bdd-ui-interactions.md §2.31: prefix matching is case-sensitive - a lowercase-diverging prefix matches nothing, since cnl-grammar.md's verbs are exact-case literals.</summary>
    [Fact]
    public void GetCompletions_LowercasePrefix_IsCaseSensitive_ReturnsEmpty()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "summariz";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length);

        Assert.Empty(completions);
    }

    /// <summary>bdd-ui-interactions.md §2.32: completions recompute after each keystroke, all narrowing to the single matching candidate as more of it is typed.</summary>
    [Theory]
    [InlineData("S")]
    [InlineData("Su")]
    [InlineData("Sum")]
    public void GetCompletions_ProgressiveTyping_AllNarrowToSummarize(string text)
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        var root = parserHost.Parse(text);
        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions);
        Assert.Equal("Summarize", item.Text);
        Assert.Equal(text.Length, item.PrefixLength);
    }

    /// <summary>bdd-ui-interactions.md §2.30: a multi-word candidate ("Load the") narrows correctly across its own internal space while mid-typing.</summary>
    [Fact]
    public void GetCompletions_MultiWordCandidate_NarrowsAcrossInternalSpace()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Load th";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions);
        Assert.Equal("Load the", item.Text);
        Assert.Equal(7, item.PrefixLength);
    }

    /// <summary>bdd-ui-interactions.md §2.20: at an input position, completions include a name bound by an earlier bind_stmt.</summary>
    [Fact]
    public void GetCompletions_ResourcePosition_IncludesBoundVariableName()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Let article be the text from \"a.txt\".\nSummarize ";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions, c => c.Text == "article");
        Assert.Equal(CompletionKind.Variable, item.Kind);
    }

    /// <summary>bdd-ui-interactions.md §2.21: variables rank above pronouns per ui-intellisense-engine-spec.md §5.3.</summary>
    [Fact]
    public void GetCompletions_VariableAndPronounBothValid_VariableRanksFirst()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Let article be the text from \"a.txt\".\nSummarize ";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var variableIndex = completions.FindIndex(c => c.Kind == CompletionKind.Variable);
        var pronounIndex = completions.FindIndex(c => c.Kind == CompletionKind.Pronoun);
        Assert.True(variableIndex >= 0 && pronounIndex >= 0, "expected both a variable and a pronoun candidate");
        Assert.True(variableIndex < pronounIndex, "expected the variable to rank before any pronoun");
    }

    /// <summary>bdd-ui-interactions.md §2.22: right after "using ", completions include a structural prompt-hole skeleton.</summary>
    [Fact]
    public void GetCompletions_AfterUsing_IncludesPromptHoleSkeleton()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Summarize it using ";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions, c => c.Kind == CompletionKind.PromptTemplate);
        Assert.Equal("{{ prompt: \"\" }}", item.Text);
    }

    /// <summary>bdd-ui-interactions.md §2.20: a name bound AFTER the cursor is never suggested.</summary>
    [Fact]
    public void GetCompletions_VariableBoundAfterCursor_IsNotSuggested()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Summarize \nLet article be the text from \"a.txt\".";
        var cursorByte = "Summarize ".Length;
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, cursorByte);

        Assert.DoesNotContain(completions, c => c.Text == "article");
    }

    /// <summary>bdd-ui-interactions.md §2.23: the "Load the" verb completion carries a full sentence skeleton, cursor positioned right before "from" (the first blank after the verb literal).</summary>
    [Fact]
    public void GetCompletions_LoadVerb_CarriesSentenceSkeletonWithCursorBeforeFrom()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Load";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions, c => c.Text == "Load the");
        Assert.Equal("Load the  from \"\".", item.SnippetText);
        Assert.Equal("Load the  from \"\".".IndexOf("from", StringComparison.Ordinal), item.SnippetCursorOffset);
    }

    /// <summary>bdd-ui-interactions.md §2.23: the "Summarize" verb completion's skeleton has no separate blank markup - the cursor sits right before the sentence-terminating period.</summary>
    [Fact]
    public void GetCompletions_SummarizeVerb_CarriesSentenceSkeletonWithCursorBeforePeriod()
    {
        using var parserHost = new ParserHost();
        var completionService = new CompletionService();

        const string text = "Summariz";
        var root = parserHost.Parse(text);

        var completions = completionService.GetCompletions(text, root, text.Length).ToList();

        var item = Assert.Single(completions, c => c.Text == "Summarize");
        Assert.Equal("Summarize .", item.SnippetText);
        Assert.Equal("Summarize .".IndexOf('.'), item.SnippetCursorOffset);
    }

    /// <summary>bdd-ui-interactions.md §2.23's own AS MEASURED BY: the inserted skeleton reparses with zero ERROR/MISSING nodes once its blanks are filled in.</summary>
    [Fact]
    public void LoadSkeleton_WithBlanksFilled_ReparsesWithNoErrorOrMissingNodes()
    {
        using var parserHost = new ParserHost();

        var root = parserHost.Parse("Load the article from \"a.txt\".");

        Assert.False(HasErrorDescendant(root));
    }
}
