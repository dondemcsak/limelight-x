using System.Runtime.InteropServices;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. App-wide singleton, owning a
/// private ParserHost dedicated to trial-insertion reparses (same precedent
/// as CompletionService/CnlSyntaxColorizer).
///
/// The validity probe is NOT always just opener+closer: `""` alone is a
/// complete, valid (empty) `string` node, but bare `{{}}` is never a valid
/// `prompt_hole` - that rule requires a literal "prompt:" and a string
/// between the braces (`seq("{{", "prompt:", $.string, "}}")`). So the
/// `{{` probe inserts a full minimal skeleton (`{{prompt:""}}`) instead of
/// just the pair, to confirm a prompt hole belongs at this position at all;
/// the actual auto-close action (in CnlEditor) only ever inserts the closer
/// itself, never the probe text.
/// </summary>
public sealed class AutoPairService : IAutoPairService
{
    private static readonly Dictionary<string, (string Closer, string ValidityProbe, string ExpectedNodeType)> Pairs = new()
    {
        ["\""] = ("\"", "\"\"", "string"),
        ["{{"] = ("}}", "{{prompt:\"\"}}", "prompt_hole"),
    };

    private readonly ParserHost _parserHost = new();

    public bool CanAutoClose(string text, TSNode root, int cursorByte, string opener)
    {
        if (!Pairs.TryGetValue(opener, out var pair))
        {
            return false;
        }

        var utf8Text = new Utf8Text(text);
        var cursorChar = utf8Text.ByteOffsetToCharOffset(cursorByte);
        var trialText = text[..cursorChar] + pair.ValidityProbe + text[cursorChar..];
        var trialRoot = _parserHost.Parse(trialText);

        return HasDescendant(trialRoot, n =>
            (int)NativeMethods.ts_node_start_byte(n) == cursorByte && NodeType(n) == pair.ExpectedNodeType);
    }

    private static bool HasDescendant(TSNode node, Func<TSNode, bool> predicate)
    {
        if (predicate(node))
        {
            return true;
        }

        var count = NativeMethods.ts_node_child_count(node);
        for (uint i = 0; i < count; i++)
        {
            if (HasDescendant(NativeMethods.ts_node_child(node, i), predicate))
            {
                return true;
            }
        }

        return false;
    }

    private static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? string.Empty;
}
