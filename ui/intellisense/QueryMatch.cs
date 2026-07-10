namespace LimelightX.UI.Intellisense;

/// <summary>One capture from an IQueryRunner query (spec/parsing/tree-sitter-integration.md §6) - a query-defined capture name (e.g. "keyword", "string") plus the matched node's UTF-8 byte span.</summary>
public readonly record struct QueryMatch(string Capture, int StartByte, int EndByte);
