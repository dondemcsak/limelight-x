using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. Walks every descendant of the
/// given root (no query needed - ts_node_is_error/is_missing are structural
/// node properties, not grammar-defined captures) and yields one
/// LocalDiagnostic per ERROR/MISSING node (bdd-ui-interactions.md
/// §2.7-§2.8). Advisory only - never writes to EditorViewModel.SyntaxErrors.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    public IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root)
    {
        foreach (var node in DescendantsAndSelf(root))
        {
            if (NativeMethods.ts_node_is_missing(node))
            {
                yield return new LocalDiagnostic("Missing expected token.", (int)NativeMethods.ts_node_start_byte(node), (int)NativeMethods.ts_node_end_byte(node));
            }
            else if (NativeMethods.ts_node_is_error(node))
            {
                yield return new LocalDiagnostic("Unexpected token.", (int)NativeMethods.ts_node_start_byte(node), (int)NativeMethods.ts_node_end_byte(node));
            }
        }
    }

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
