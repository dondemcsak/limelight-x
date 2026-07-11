using LimelightX.UI.Intellisense;
using Xunit;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>
/// Real Tree-sitter-backed tests - no fakes, exercises the concrete
/// NavigationService against the actual grammar/DLLs.
/// </summary>
[Trait("Category", "NativeArm64")]
public class NavigationServiceTests
{
    /// <summary>bdd-ui-interactions.md §2.26: a reference to a bound name resolves to that name's bind_stmt span.</summary>
    [Fact]
    public void FindDefinition_ReferenceAfterBinding_ReturnsBindStmtSpan()
    {
        using var parserHost = new ParserHost();
        var navigationService = new NavigationService();

        const string text = "Let article be the text from \"a.txt\".\nSummarize article.";
        var referenceByte = text.LastIndexOf("article", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var span = navigationService.FindDefinition(text, root, referenceByte);

        Assert.NotNull(span);
        Assert.Equal(0, span!.Value.Start);
        var expectedEnd = text.IndexOf('\n');
        Assert.Equal(expectedEnd, span.Value.End);
    }

    /// <summary>bdd-ui-interactions.md §2.26: no preceding binding for the name at the cursor returns null.</summary>
    [Fact]
    public void FindDefinition_NoPrecedingBinding_ReturnsNull()
    {
        using var parserHost = new ParserHost();
        var navigationService = new NavigationService();

        const string text = "Summarize the article.";
        var referenceByte = text.IndexOf("article", StringComparison.Ordinal);
        var root = parserHost.Parse(text);

        var span = navigationService.FindDefinition(text, root, referenceByte);

        Assert.Null(span);
    }
}
