using System.Runtime.InteropServices;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. App-wide singleton: loads and
/// compiles the three .scm queries once at construction (from the output
/// directory's queries/ folder, copied there from ui/queries/ by .csproj),
/// caches each as a native TSQuery for the lifetime of this instance, and
/// runs a fresh cursor per call against whichever tab's TSNode root it's
/// given. Dispose frees the three cached TSQuery handles - callers that own
/// a QueryRunner (composition root, CnlSyntaxColorizer) must dispose it
/// alongside their own native handles.
/// </summary>
public sealed class QueryRunner : IQueryRunner
{
    private readonly IntPtr _language;
    private readonly IntPtr _highlightsQuery;
    private readonly IntPtr _foldsQuery;
    private readonly IntPtr _injectionsQuery;

    public QueryRunner()
    {
        _language = NativeMethods.tree_sitter_limelightx();
        _highlightsQuery = LoadQuery("highlights.scm");
        _foldsQuery = LoadQuery("folds.scm");
        _injectionsQuery = LoadQuery("injections.scm");
    }

    public IEnumerable<QueryMatch> RunHighlights(TSNode root) => Run(_highlightsQuery, root);

    public IEnumerable<QueryMatch> RunFolds(TSNode root) => Run(_foldsQuery, root);

    public IEnumerable<QueryMatch> RunInjections(TSNode root) => Run(_injectionsQuery, root);

    private IntPtr LoadQuery(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "queries", fileName);
        var source = File.ReadAllBytes(path);

        var query = NativeMethods.ts_query_new(_language, source, (uint)source.Length, out var errorOffset, out var errorType);
        if (errorType != TSQueryError.None)
        {
            throw new InvalidOperationException($"Failed to compile {fileName}: {errorType} at byte offset {errorOffset}.");
        }

        return query;
    }

    private List<QueryMatch> Run(IntPtr query, TSNode root)
    {
        var results = new List<QueryMatch>();
        var cursor = NativeMethods.ts_query_cursor_new();

        try
        {
            NativeMethods.ts_query_cursor_exec(cursor, query, root);

            while (NativeMethods.ts_query_cursor_next_capture(cursor, out var match, out var captureIndex))
            {
                var captureSize = Marshal.SizeOf<TSQueryCapture>();
                var capturePtr = match.Captures + (int)(captureIndex * captureSize);
                var capture = Marshal.PtrToStructure<TSQueryCapture>(capturePtr);

                var namePtr = NativeMethods.ts_query_capture_name_for_id(query, capture.Index, out var nameLength);
                var name = Marshal.PtrToStringUTF8(namePtr, (int)nameLength)
                    ?? throw new InvalidOperationException($"ts_query_capture_name_for_id returned null for capture index {capture.Index}.");

                var startByte = (int)NativeMethods.ts_node_start_byte(capture.Node);
                var endByte = (int)NativeMethods.ts_node_end_byte(capture.Node);
                results.Add(new QueryMatch(name, startByte, endByte));
            }
        }
        finally
        {
            NativeMethods.ts_query_cursor_delete(cursor);
        }

        return results;
    }

    public void Dispose()
    {
        NativeMethods.ts_query_delete(_highlightsQuery);
        NativeMethods.ts_query_delete(_foldsQuery);
        NativeMethods.ts_query_delete(_injectionsQuery);
    }
}
