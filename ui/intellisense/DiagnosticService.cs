using System.Runtime.InteropServices;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. Walks every descendant of the
/// given root (no query needed - ts_node_is_error/is_missing are structural
/// node properties, not grammar-defined captures) and yields one
/// LocalDiagnostic per ERROR/MISSING node (bdd-ui-interactions.md
/// §2.7-§2.8). Advisory only - never writes to EditorViewModel.SyntaxErrors.
/// For MISSING nodes whose expected literal is one of a fixed, narrow set of
/// self-describing tokens (ui-intellisense-engine-spec.md §6.1), also
/// derives a specific message and SuggestedFix, driving ghost-text
/// (bdd-ui-interactions.md §2.18). This table is exhaustive for v1 - do not
/// extend without explicit instruction (CLAUDE.md §3.2). Also flags a
/// structurally-valid `pronoun` node with no preceding sentence to refer to
/// (bdd-ui-interactions.md §2.28) - reuses PronounReferenceResolver, the same
/// nearest-preceding-sentence check HoverService's §7.2 pronoun hover already
/// performs; a real bug in the user's CNL, not a grammar violation, so it
/// carries no SuggestedFix.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    private static readonly Dictionary<string, (string Message, string Fix)> SelfDescribingMissingLiterals = new()
    {
        ["."] = ("Missing period at end of sentence.", "."),
        ["\""] = ("Missing closing quote.", "\""),
        ["}}"] = ("Missing closing '}}' for expression hole.", "}}"),
    };

    public IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root)
    {
        foreach (var node in DescendantsAndSelf(root))
        {
            if (NativeMethods.ts_node_is_missing(node))
            {
                var start = (int)NativeMethods.ts_node_start_byte(node);
                var end = (int)NativeMethods.ts_node_end_byte(node);

                yield return SelfDescribingMissingLiterals.TryGetValue(NodeType(node), out var known)
                    ? new LocalDiagnostic(known.Message, start, end, known.Fix)
                    : new LocalDiagnostic("Missing expected token.", start, end);
            }
            else if (NativeMethods.ts_node_is_error(node))
            {
                yield return new LocalDiagnostic("Unexpected token.", (int)NativeMethods.ts_node_start_byte(node), (int)NativeMethods.ts_node_end_byte(node));
            }
            else if (NodeType(node) == "pronoun" && PronounReferenceResolver.PrecedingSentence(node) is null)
            {
                yield return new LocalDiagnostic("Pronoun has no preceding sentence to refer to.", (int)NativeMethods.ts_node_start_byte(node), (int)NativeMethods.ts_node_end_byte(node));
            }
        }
    }

    private static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? string.Empty;

    private static IEnumerable<TSNode> DescendantsAndSelf(TSNode node)
    {
        yield return node;

        var count = NativeMethods.ts_node_child_count(node);
        for (uint i = 0; i < count; i++)
        {
            foreach (var descendant in DescendantsAndSelf(NativeMethods.ts_node_child(node, i)))
            {
                yield return descendant;
            }
        }
    }
}
