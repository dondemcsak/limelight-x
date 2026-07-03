using LimelightX.UI.Components;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

public class SyntaxHighlighterTests
{
    [Fact]
    public void Tokenize_LoadStatement_ClassifiesKeywordsResourceAndString()
    {
        const string source = "Load the article from \"article.txt\".";
        var tokens = SyntaxHighlighter.Tokenize(source);

        AssertToken(tokens, source, "Load", TokenKind.Keyword);
        AssertToken(tokens, source, "the", TokenKind.Keyword);
        AssertToken(tokens, source, "article", TokenKind.Resource);
        AssertToken(tokens, source, "from", TokenKind.Keyword);
        AssertToken(tokens, source, "\"article.txt\"", TokenKind.String);
    }

    [Fact]
    public void Tokenize_SinglePronoun_ClassifiesAsPronoun()
    {
        const string source = "Summarize it.";
        var tokens = SyntaxHighlighter.Tokenize(source);

        AssertToken(tokens, source, "Summarize", TokenKind.Keyword);
        AssertToken(tokens, source, "it", TokenKind.Pronoun);
    }

    [Theory]
    [InlineData("Summarize the result.", "the result")]
    [InlineData("Summarize the output.", "the output")]
    public void Tokenize_PronounPhrase_ClassifiesBothWordsAsPronoun(string source, string phrase)
    {
        var tokens = SyntaxHighlighter.Tokenize(source);
        var words = phrase.Split(' ');

        AssertToken(tokens, source, words[0], TokenKind.Pronoun);
        AssertToken(tokens, source, words[1], TokenKind.Pronoun);
    }

    [Fact]
    public void Tokenize_StandaloneThe_IsKeywordNotPronoun()
    {
        const string source = "Extract the entities.";
        var tokens = SyntaxHighlighter.Tokenize(source);

        AssertToken(tokens, source, "the", TokenKind.Keyword);
        AssertToken(tokens, source, "entities", TokenKind.Resource);
    }

    [Fact]
    public void Tokenize_ExpressionHole_ClassifiesBracesAsHoleAndNestedStringAsString()
    {
        const string source = "Rewrite the summary using {{ prompt: \"Be concise.\" }}.";
        var tokens = SyntaxHighlighter.Tokenize(source);

        var holeStart = source.IndexOf("{{", StringComparison.Ordinal);
        var stringStart = source.IndexOf("\"Be concise.\"", StringComparison.Ordinal);

        Assert.Contains(tokens, t => t.Start == holeStart && t.Kind == TokenKind.ExpressionHole);
        AssertToken(tokens, source, "\"Be concise.\"", TokenKind.String);
        Assert.Contains(tokens, t => t.Start == stringStart - 1 && t.Kind == TokenKind.ExpressionHole); // space before the string, inside the hole
    }

    [Fact]
    public void Tokenize_UnterminatedString_ConsumesToEndOfText()
    {
        const string source = "Load the article from \"article.txt";
        var tokens = SyntaxHighlighter.Tokenize(source);

        var stringToken = tokens.Single(t => t.Kind == TokenKind.String);
        Assert.Equal(source.Length, stringToken.End);
    }

    [Fact]
    public void Tokenize_TokensAreContiguousAndCoverEntireText()
    {
        const string source = "Translate it to French using {{ prompt: \"Be formal.\" }}.";
        var tokens = SyntaxHighlighter.Tokenize(source);

        Assert.Equal(0, tokens[0].Start);
        Assert.Equal(source.Length, tokens[^1].End);
        for (var i = 1; i < tokens.Count; i++)
        {
            Assert.Equal(tokens[i - 1].End, tokens[i].Start);
        }
    }

    private static void AssertToken(IReadOnlyList<Token> tokens, string source, string expectedText, TokenKind expectedKind)
    {
        var start = source.IndexOf(expectedText, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{expectedText}' not found in source");
        var end = start + expectedText.Length;

        Assert.Contains(tokens, t => t.Start == start && t.End == end && t.Kind == expectedKind);
    }
}
