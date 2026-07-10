using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// HoverService against the actual grammar/DLLs.
/// </summary>
[Trait("Category", "NativeArm64")]
public class HoverServiceTests
{
    /// <summary>ui-intellisense-engine-spec.md §7.3: hovering a verb reports its description, matching the spec's own example text.</summary>
    [Fact]
    public void GetHover_OverVerbToken_ReturnsVerbDescription()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string text = "Summarize it.";
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, 0);

        Assert.NotNull(hover);
        Assert.Equal("Summarize: Reduce text to a shorter form.", hover!.Text);
        Assert.Equal(0, hover.Position);
    }

    /// <summary>bdd-ui-interactions.md §2.11: non-verb structural keywords (no §7.3 description exists for them) keep the plain grammar-role label.</summary>
    [Fact]
    public void GetHover_OverStructuralKeywordToken_ReturnsKeywordRole()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string text = "Load the article from \"a.txt\".";
        var cursorByte = text.IndexOf("from", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, cursorByte);

        Assert.NotNull(hover);
        Assert.Equal("keyword", hover!.Text);
        Assert.Equal(cursorByte, hover.Position);
    }

    /// <summary>bdd-ui-interactions.md §2.11: with no preceding sentence to reference, a pronoun keeps the plain grammar-role label.</summary>
    [Fact]
    public void GetHover_OverPronounTokenWithNoPrecedingSentence_ReturnsPronounRole()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string text = "Summarize it.";
        var cursorByte = text.IndexOf("it", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, cursorByte);

        Assert.NotNull(hover);
        Assert.Equal("pronoun", hover!.Text);
        Assert.Equal(cursorByte, hover.Position);
    }

    /// <summary>ui-intellisense-engine-spec.md §7.2: with a preceding sentence, a pronoun reports it by kind and line, matching the spec's own example format.</summary>
    [Fact]
    public void GetHover_OverPronounTokenWithPrecedingSentence_ReturnsReferencePreview()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string text = "Summarize it.\nRewrite it.";
        var cursorByte = text.LastIndexOf("it", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, cursorByte);

        Assert.NotNull(hover);
        Assert.Equal("Pronoun refers to: SummarizeStmt at line 1", hover!.Text);
        Assert.Equal(cursorByte, hover.Position);
    }

    /// <summary>ui-intellisense-engine-spec.md §7.1: hovering a reference to a previously-bound variable shows its binding sentence.</summary>
    [Fact]
    public void GetHover_OverVariableReferenceMatchingPriorBinding_ReturnsBindingSentence()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string bindingSentence = "Let article be the text from \"article.txt\".";
        var text = bindingSentence + "\nSummarize article.";
        var cursorByte = text.LastIndexOf("article", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, cursorByte);

        Assert.NotNull(hover);
        Assert.Equal(bindingSentence, hover!.Text);
        Assert.Equal(cursorByte, hover.Position);
    }

    /// <summary>ui-intellisense-engine-spec.md §7.4: hovering an expression hole reports its template description.</summary>
    [Fact]
    public void GetHover_OverPromptHole_ReturnsTemplateDescription()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string text = "Summarize it using {{ prompt: \"Summarize in 3 bullets.\" }}.";
        var cursorByte = text.IndexOf("{{", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, cursorByte);

        Assert.NotNull(hover);
        Assert.Equal("Expression hole: embeds a literal prompt for the model.", hover!.Text);
        Assert.Equal(cursorByte, hover.Position);
    }

    /// <summary>bdd-ui-interactions.md §2.11: hovering whitespace between tokens returns null.</summary>
    [Fact]
    public void GetHover_OverWhitespaceBetweenTokens_ReturnsNull()
    {
        using var parserHost = new ParserHost();
        var hoverService = new HoverService();

        const string text = "Summarize it.";
        var whitespaceByte = text.IndexOf(' ');
        var root = parserHost.Parse(text);

        var hover = hoverService.GetHover(text, root, whitespaceByte);

        Assert.Null(hover);
    }
}
