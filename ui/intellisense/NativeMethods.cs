using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Lets ui/tests/Intellisense/*Tests.cs walk real TSNode trees directly
// (ts_node_type/child/etc.) against the real DLLs, without promoting
// NativeMethods to a public surface other consumers could depend on.
[assembly: InternalsVisibleTo("LimelightX.UI.Tests")]

namespace LimelightX.UI.Intellisense;

/// <summary>Tree-sitter's TSQueryError (tree_sitter/api.h).</summary>
public enum TSQueryError
{
    None = 0,
    Syntax,
    NodeType,
    Field,
    Capture,
    Structure,
    Language,
}

/// <summary>Tree-sitter's TSQueryCapture (tree_sitter/api.h) - one captured node plus its capture-name index.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TSQueryCapture
{
    public readonly TSNode Node;
    public readonly uint Index;
}

/// <summary>Tree-sitter's TSQueryMatch (tree_sitter/api.h). Captures is a native array of TSQueryCapture, length CaptureCount.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TSQueryMatch
{
    public readonly uint Id;
    public readonly ushort PatternIndex;
    public readonly ushort CaptureCount;
    public readonly IntPtr Captures;
}

/// <summary>
/// Raw P/Invoke bindings against two separate native DLLs - see
/// spec/parsing/tree-sitter-runtime-build-guide.md §5 for why there are two:
/// `tree-sitter-runtime.dll` (the actual Tree-sitter parsing/tree/node/query
/// engine, built from tree-sitter core's lib/) and `tree-sitter-limelightx.dll`
/// (the CNL grammar only, exporting a single tree_sitter_limelightx()
/// accessor). One consolidated file, not scattered per-service declarations,
/// so there's exactly one place a signature can get wrong (cnl-editor-architecture.md
/// implementation plan, "Deferred to a later plan" section).
///
/// bool return values use [MarshalAs(UnmanagedType.I1)]: tree-sitter's C API
/// uses C99 bool (1 byte, stdbool.h), not the 4-byte Win32 BOOL .NET's
/// default marshaler assumes for `bool` - getting this wrong silently
/// misreads 3 of every 4 bytes of the next return value/field.
/// </summary>
internal static class NativeMethods
{
    private const string RuntimeDll = "tree-sitter-runtime.dll";
    private const string GrammarDll = "tree-sitter-limelightx.dll";

    [DllImport(GrammarDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr tree_sitter_limelightx();

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ts_parser_new();

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ts_parser_delete(IntPtr parser);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ts_parser_set_language(IntPtr parser, IntPtr language);

    /// <summary>
    /// `source`/`length` must be a UTF-8 byte buffer and its UTF-8 byte
    /// length - NOT a .NET string's UTF-16 char count. Always go through
    /// Utf8Text rather than calling this directly. `source` is a managed
    /// byte[], which the CLR marshaler pins for the duration of the call -
    /// no manual Marshal.AllocHGlobal needed.
    /// </summary>
    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ts_parser_parse_string(IntPtr parser, IntPtr oldTree, byte[] source, uint length);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern TSNode ts_tree_root_node(IntPtr tree);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ts_tree_delete(IntPtr tree);

    /// <summary>Returns a pointer to a short, static, ASCII/UTF-8 node-type name (e.g. "sentence", "ERROR") - use Marshal.PtrToStringUTF8, never free it.</summary>
    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ts_node_type(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ts_node_start_byte(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ts_node_end_byte(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern TSPoint ts_node_start_point(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern TSPoint ts_node_end_point(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern TSNode ts_node_child(TSNode node, uint index);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ts_node_child_count(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern TSNode ts_node_parent(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern TSNode ts_node_descendant_for_byte_range(TSNode node, uint start, uint end);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ts_node_is_error(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ts_node_is_missing(TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ts_node_is_null(TSNode node);

    /// <summary>`source`/`sourceLen` is the .scm query text as UTF-8 bytes (query source is plain ASCII/UTF-8 in this project, but the API still wants a byte buffer + byte length, not a char count).</summary>
    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ts_query_new(IntPtr language, byte[] source, uint sourceLen, out uint errorOffset, out TSQueryError errorType);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ts_query_delete(IntPtr query);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ts_query_cursor_new();

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ts_query_cursor_delete(IntPtr cursor);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ts_query_cursor_exec(IntPtr cursor, IntPtr query, TSNode node);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ts_query_cursor_next_match(IntPtr cursor, out TSQueryMatch match);

    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ts_query_cursor_next_capture(IntPtr cursor, out TSQueryMatch match, out uint captureIndex);

    /// <summary>Returns a pointer to the capture's name (e.g. the "keyword" in "@keyword"), NOT null-terminated - always read exactly `length` bytes via Marshal.PtrToStringUTF8(ptr, (int)length) or equivalent.</summary>
    [DllImport(RuntimeDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ts_query_capture_name_for_id(IntPtr query, uint index, out uint length);
}
