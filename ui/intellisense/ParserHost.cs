namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. Owns one native TSParser (created
/// once, freed on Dispose) and one native TSTree (replaced, with the
/// previous one freed, on every Parse call). Each Parse is a full reparse
/// (old_tree = IntPtr.Zero) rather than an incremental one - Tree-sitter
/// supports incremental reparsing via ts_tree_edit for performance, but
/// nothing in this codebase tracks edit ranges yet, so a full reparse per
/// call is the correct, simple starting point.
/// </summary>
public sealed class ParserHost : IParserHost
{
    private readonly IntPtr _parser;
    private IntPtr _tree;

    public ParserHost()
    {
        _parser = NativeMethods.ts_parser_new();
        var language = NativeMethods.tree_sitter_limelightx();
        NativeMethods.ts_parser_set_language(_parser, language);
    }

    public TSNode Parse(string text)
    {
        var utf8 = new Utf8Text(text);
        var newTree = NativeMethods.ts_parser_parse_string(_parser, IntPtr.Zero, utf8.Bytes, utf8.ByteLength);

        if (_tree != IntPtr.Zero)
        {
            NativeMethods.ts_tree_delete(_tree);
        }

        _tree = newTree;
        return NativeMethods.ts_tree_root_node(_tree);
    }

    public void Dispose()
    {
        if (_tree != IntPtr.Zero)
        {
            NativeMethods.ts_tree_delete(_tree);
        }

        NativeMethods.ts_parser_delete(_parser);
    }
}
