using System.Text;

namespace LimelightX.UI.Components;

/// <summary>
/// Deterministic, rule-based CNL tokenizer (ui-components.md §3.2,
/// ui-styling-theming.md §5). Hand-coded once directly from
/// spec/cnl-grammar.md's sentence patterns and pronoun list, per the plan's
/// resolution of the "runtime vs. build-time vs. hand-coded" grammar-parsing
/// ambiguity - CNL's v0.1 surface area (§7 Non-Goals: no nested clauses, no
/// arbitrary natural language) is small and fixed enough that a hand-coded
/// scanner is both correct and far simpler than any grammar-driven tooling,
/// none of which is on the approved dependency list anyway.
///
/// No heuristics: every character position is classified by a fixed rule
/// (quoted string / expression hole / exact-case keyword or pronoun match /
/// default free-text "Resource") - the same input always produces the same
/// tokens, and nothing is guessed contextually.
/// </summary>
public static class SyntaxHighlighter
{
    // Sentence-initial verbs are capitalized; mid-sentence structural words are
    // lowercase - both forms exactly as they appear in spec/cnl-grammar.md §2, §5.
    private static readonly HashSet<string> Keywords =
    [
        "Load", "Extract", "Summarize", "Translate", "Let", "Rewrite", "Format",
        "from", "to", "be", "using", "as", "the",
    ];

    // spec/cnl-grammar.md §3. "the result"/"the output" are two-word phrases
    // checked ahead of the standalone "the" keyword; the rest are single words.
    private static readonly HashSet<string> SingleWordPronouns = ["it", "them", "this", "that"];
    private static readonly HashSet<string> PronounPhraseSecondWords = ["result", "output"];

    public static IReadOnlyList<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '"')
            {
                var end = FindStringEnd(text, i);
                tokens.Add(new Token(i, end, TokenKind.String));
                i = end;
            }
            else if (c == '{' && i + 1 < text.Length && text[i + 1] == '{')
            {
                i = TokenizeExpressionHole(text, i, tokens);
            }
            else if (char.IsLetter(c))
            {
                i = TokenizeWord(text, i, tokens);
            }
            else
            {
                tokens.Add(new Token(i, i + 1, TokenKind.Plain));
                i++;
            }
        }

        return tokens;
    }

    private static int FindStringEnd(string text, int start)
    {
        var j = start + 1;
        while (j < text.Length && text[j] != '"')
        {
            j++;
        }

        // Unterminated string (parse error upstream) - consume to end of text
        // rather than leaving the rest of the line unclassified.
        return j < text.Length ? j + 1 : text.Length;
    }

    private static int TokenizeExpressionHole(string text, int start, List<Token> tokens)
    {
        var closeIndex = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
        var end = closeIndex >= 0 ? closeIndex + 2 : text.Length;

        // Rule: the prompt must be a quoted string (spec/cnl-grammar.md §4) - so a
        // nested string, if present, keeps its own String coloring; every other
        // character in the hole (the "{{", "prompt:", and "}}" glyphs) is ExpressionHole.
        var i = start;
        while (i < end)
        {
            if (text[i] == '"')
            {
                var stringEnd = Math.Min(FindStringEnd(text, i), end);
                tokens.Add(new Token(i, stringEnd, TokenKind.String));
                i = stringEnd;
            }
            else
            {
                tokens.Add(new Token(i, i + 1, TokenKind.ExpressionHole));
                i++;
            }
        }

        return end;
    }

    private static int TokenizeWord(string text, int start, List<Token> tokens)
    {
        var end = start;
        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        var word = text[start..end];

        if (word == "the" && TryMatchPronounPhrase(text, end, out var secondWordStart, out var secondWordEnd))
        {
            tokens.Add(new Token(start, end, TokenKind.Pronoun));
            if (secondWordStart > end)
            {
                // Whitespace between "the" and "result"/"output" stays Plain;
                // only word glyphs carry color, matching every other word case below.
                tokens.Add(new Token(end, secondWordStart, TokenKind.Plain));
            }

            tokens.Add(new Token(secondWordStart, secondWordEnd, TokenKind.Pronoun));
            return secondWordEnd;
        }

        var kind = word switch
        {
            _ when Keywords.Contains(word) => TokenKind.Keyword,
            _ when SingleWordPronouns.Contains(word) => TokenKind.Pronoun,
            _ => TokenKind.Resource,
        };

        tokens.Add(new Token(start, end, kind));
        return end;
    }

    private static bool TryMatchPronounPhrase(string text, int afterThe, out int secondWordStart, out int secondWordEnd)
    {
        var j = afterThe;
        while (j < text.Length && char.IsWhiteSpace(text[j]))
        {
            j++;
        }

        secondWordStart = j;
        while (j < text.Length && char.IsLetter(text[j]))
        {
            j++;
        }

        secondWordEnd = j;
        return secondWordEnd > secondWordStart && PronounPhraseSecondWords.Contains(text[secondWordStart..secondWordEnd]);
    }
}
