# Limelight‑X IntelliSense Engine Implementation Guide  
## Version 1.0 — July 2026

This guide provides the **implementation details** for building the Limelight‑X IntelliSense Engine  
on top of the native Tree‑sitter grammar DLL (`tree-sitter-limelightx.dll`).  
It is intended for Coding Assistants and future maintainers responsible for implementing  
editor‑side features such as completions, diagnostics, hovers, folding, and structural navigation.

This guide complements the IntelliSense Engine Specification and the Coding Assistant Onboarding Document, and is subordinate to both that document and `spec/cnl-editor-architecture.md` (parent authority).

---

# 1. Implementation Overview

The IntelliSense Engine is implemented as a set of C# services inside the Avalonia UI layer, in `/ui/intellisense` (approved directory, `CLAUDE.md` §1):

```
ui/intellisense/
    CompletionService.cs
    DiagnosticService.cs
    HoverService.cs
    FoldingService.cs
    OutlineService.cs
    QueryRunner.cs
    ParserHost.cs
```

These services populate `EditorViewModel`'s existing `CompletionItems`, `HoverInfo`, and `QuickFixes` state (`spec/ux/ui-viewmodels.md` §6; `ui/viewmodels/{CompletionItem,HoverInfo,QuickFixItem}.cs`) — see §4.2 and §6.1 below for exactly how each service's return type maps onto those existing types, rather than inventing new, colliding ones.

Each service consumes:

- The CST produced by Tree‑sitter  
- Query results from `.scm` files  
- Cursor position  
- Document text  

The engine must **never** modify text before sending it to the Rust backend, and never calls the Rust backend or `/src/api` at all — it has no channel to do so (`cnl-editor-architecture.md` §5).

---

# 2. ParserHost Implementation

The `ParserHost` is responsible for:

- Loading the DLL  
- Creating the parser  
- Parsing text  
- Managing tree lifecycle  

## 2.1 P/Invoke Bindings

```csharp
[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr tree_sitter_limelightx();

[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr ts_parser_new();

[DllImport("tree-sitter-limelightx.dll")]
public static extern bool ts_parser_set_language(IntPtr parser, IntPtr language);

[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr ts_parser_parse_string(
    IntPtr parser,
    IntPtr oldTree,
    string input,
    UIntPtr length);

[DllImport("tree-sitter-limelightx.dll")]
public static extern void ts_tree_delete(IntPtr tree);

[DllImport("tree-sitter-limelightx.dll")]
public static extern void ts_parser_delete(IntPtr parser);
```

## 2.2 ParserHost Class

```csharp
public sealed class ParserHost : IDisposable
{
    private readonly IntPtr _parser;
    private readonly IntPtr _language;

    public ParserHost()
    {
        _parser = ts_parser_new();
        _language = tree_sitter_limelightx();
        ts_parser_set_language(_parser, _language);
    }

    public IntPtr Parse(string text)
    {
        return ts_parser_parse_string(
            _parser,
            IntPtr.Zero,
            text,
            (UIntPtr)text.Length);
    }

    public void Dispose()
    {
        ts_parser_delete(_parser);
    }
}
```

---

# 3. QueryRunner Implementation

The QueryRunner loads `.scm` files and executes Tree‑sitter queries.

## 3.1 P/Invoke Bindings

```csharp
[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr ts_query_new(
    IntPtr language,
    string source,
    UIntPtr length,
    out UIntPtr errorOffset,
    out TSQueryError error);

[DllImport("tree-sitter-limelightx.dll")]
public static extern void ts_query_delete(IntPtr query);

[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr ts_query_cursor_new();

[DllImport("tree-sitter-limelightx.dll")]
public static extern void ts_query_cursor_exec(
    IntPtr cursor,
    IntPtr query,
    TSNode node);

[DllImport("tree-sitter-limelightx.dll")]
public static extern void ts_query_cursor_delete(IntPtr cursor);
```

## 3.2 QueryRunner Class

```csharp
public sealed class QueryRunner
{
    private readonly IntPtr _language;

    public QueryRunner(IntPtr language)
    {
        _language = language;
    }

    public IEnumerable<QueryMatch> Run(string scmPath, TSNode root)
    {
        var source = File.ReadAllText(scmPath);

        UIntPtr errorOffset;
        TSQueryError error;

        var query = ts_query_new(
            _language,
            source,
            (UIntPtr)source.Length,
            out errorOffset,
            out error);

        if (error != TSQueryError.None)
            throw new QueryException(scmPath, error, errorOffset);

        var cursor = ts_query_cursor_new();
        ts_query_cursor_exec(cursor, query, root);

        foreach (var match in EnumerateMatches(cursor))
            yield return match;

        ts_query_cursor_delete(cursor);
        ts_query_delete(query);
    }
}
```

---

# 4. CompletionService Implementation

The CompletionService uses CST context to determine valid next tokens.

## 4.1 Inputs

- CST root node  
- Cursor position  
- Node under cursor  
- Parent node type  

## 4.2 Implementation Outline

`GetCompletions` returns `IEnumerable<LimelightX.UI.ViewModels.CompletionItem>` directly — the same type `EditorViewModel.CompletionItems` (`ui-viewmodels.md` §6) already binds — pre-sorted per the ranking rules in `ui-intellisense-engine-spec.md` §5.3 (grammar-valid tokens first, variables above pronouns, verbs above keywords, prompt templates last). There is no separate `CompletionResult` wrapper type.

```csharp
public IEnumerable<CompletionItem> GetCompletions(TSNode root, int cursorByte)
{
    var node = FindNodeAtByte(root, cursorByte);
    var context = DetermineContext(node);

    return context switch
    {
        CompletionContext.SentenceStart => VerbCompletions(),
        CompletionContext.AfterLetBe => ResourceCompletions(),
        CompletionContext.ResourcePosition => VariableAndPronounCompletions(root),
        CompletionContext.PromptHole => PromptTemplateCompletions(),
        _ => []
    };
}
```

---

# 5. DiagnosticService Implementation

Diagnostics are derived from:

- `ERROR` nodes  
- Missing required children  
- Malformed prompt holes  
- Unknown verbs  

## 5.1 Implementation Outline

`GetDiagnostics` returns `LimelightX.UI.ViewModels.LocalDiagnostic` — `record struct LocalDiagnostic(string Message, int StartByte, int EndByte, string? SuggestedFix = null)`. For `MISSING` nodes, `ts_node_type()` gives the expected literal, looked up in a fixed table (`ui-intellisense-engine-spec.md` §6.1) to produce a specific message and, for the three self‑describing literals, a `SuggestedFix`:

```csharp
private static readonly Dictionary<string, (string Message, string Fix)> SelfDescribingMissingLiterals = new()
{
    ["."]  = ("Missing period at end of sentence.", "."),
    ["\""] = ("Missing closing quote.", "\""),
    ["}}"] = ("Missing closing '}}' for expression hole.", "}}"),
};

public IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root)
{
    foreach (var node in DescendantsAndSelf(root))
    {
        if (ts_node_is_missing(node))
        {
            var start = (int)ts_node_start_byte(node);
            var end = (int)ts_node_end_byte(node);
            var literal = ts_node_type(node);

            yield return SelfDescribingMissingLiterals.TryGetValue(literal, out var known)
                ? new LocalDiagnostic(known.Message, start, end, known.Fix)
                : new LocalDiagnostic("Missing expected token.", start, end);
        }
        else if (ts_node_is_error(node))
        {
            yield return new LocalDiagnostic("Unexpected token.", (int)ts_node_start_byte(node), (int)ts_node_end_byte(node));
        }
    }
}
```

`ts_node_is_missing`/`ts_node_is_error`/`ts_node_type`/`ts_node_start_byte`/`ts_node_end_byte` are existing P/Invoke bindings, already used elsewhere in this file and in `HoverService` — no new native binding is required. The lookup table is intentionally small and fixed (`ui-intellisense-engine-spec.md` §6.1) — do not extend it without explicit instruction.

---

# 6. HoverService Implementation

Hovers provide contextual information:

- Variable definitions  
- Pronoun reference previews  
- Verb descriptions  
- Prompt hole metadata  

## 6.1 Implementation Outline

`GetHover` returns `LimelightX.UI.ViewModels.HoverInfo?` (nullable) — the same type `EditorViewModel.HoverInfo` (`ui-viewmodels.md` §6) already binds — not a distinct type of the same name in a different namespace. `HoverInfo.Text` holds the formatted display string (e.g. the binding sentence, the "Pronoun refers to: ..." preview, or the verb description); `HoverInfo.Position` holds `cursorByte`. There is no `HoverInfo.Empty` sentinel — "no hover" is `null`, matching `EditorViewModel.HoverInfo`'s nullable, not-required binding.

```csharp
public HoverInfo? GetHover(TSNode root, int cursorByte)
{
    var node = FindNodeAtByte(root, cursorByte);

    return node.Type switch
    {
        "identifier" => VariableHover(root, node, cursorByte),
        "pronoun" => PronounHover(root, node, cursorByte),
        "verb" => VerbHover(node, cursorByte),
        "prompt_hole" => PromptHoleHover(node, cursorByte),
        _ => null
    };
}
```

`HoverService` itself stays grammar‑role‑only, as above — it has no `LocalDiagnostics` case and never will. The diagnostic‑message‑over‑grammar‑hover priority merge (`ui-intellisense-engine-spec.md` §7.5) is `EditorViewModel`'s responsibility, not `HoverService`'s, since only `EditorViewModel` holds both `LocalDiagnostics` and the `HoverService` reference:

```csharp
public void RequestHoverAt(int cursorByte)
{
    var diagnostic = LocalDiagnostics.FirstOrDefault(d => cursorByte >= d.StartByte && cursorByte <= d.EndByte);
    HoverInfo = diagnostic != default
        ? new HoverInfo { Text = diagnostic.Message, Position = diagnostic.StartByte }
        : _hoverService.GetHover(Text, _parserHost.Parse(Text), cursorByte);
}
```

---

# 7. FoldingService Implementation

Folding is driven by `.scm` fold queries.

## 7.1 Implementation Outline

```csharp
public IEnumerable<FoldRegion> GetFolds(TSNode root)
{
    var foldsPath = Path.Combine(AppContext.BaseDirectory, "queries", "folds.scm");
    return _queryRunner.Run(foldsPath, root)
        .Select(match => new FoldRegion(match.StartByte, match.EndByte));
}
```

---

# 8. OutlineService Implementation

The outline view is derived from CST sentence nodes.

## 8.1 Implementation Outline

```csharp
public IEnumerable<OutlineItem> GetOutline(TSNode root)
{
    foreach (var sentence in FindNodes(root, "sentence"))
    {
        yield return new OutlineItem
        {
            Verb = ExtractVerb(sentence),
            Resource = ExtractResource(sentence),
            Variable = ExtractVariable(sentence),
            Line = sentence.StartPoint.Row + 1
        };
    }
}
```

---

# 9. Memory Management Requirements

Every implementation must:

- Free trees after each parse  
- Free queries after each execution  
- Free cursors after each execution  
- Free parser on shutdown  

Native leaks will destabilize Avalonia.

---

# 10. Integration Rules

Coding Assistants must:

- Use P/Invoke bindings exactly as documented (§2.1, §3.1) — no third-party binding package (TreeSitterSharp or otherwise) is approved, per `CLAUDE.md` §3.5  
- Load `.scm` files from the output directory's `queries/` folder via `Path.Combine(AppContext.BaseDirectory, "queries", ...)` (copied there from `ui/queries/` by `.csproj`, `spec/parsing/tree-sitter-integration.md` §8), not a bare relative or `native/queries/`-style path  
- Never modify grammar.js without explicit instruction  
- Never generate scanner.c unless grammar requires it  
- Always treat Rust backend as authoritative  
- Populate `EditorViewModel.CompletionItems`/`HoverInfo`/`QuickFixes` using the existing types in `ui/viewmodels/`, never a new same-named or competing type (§4.2, §6.1)  

---

# 11. Summary

- This guide defines the implementation details for the IntelliSense Engine  
- Each service is isolated and deterministic  
- Tree‑sitter provides CST + queries  
- Rust backend provides semantics  
- Memory must be managed explicitly  
- Coding Assistants must follow strict rules  

This is the canonical implementation guide for the Limelight‑X IntelliSense Engine.