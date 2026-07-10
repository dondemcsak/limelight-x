# Limelight‑X Editor Services Integration Guide  
## Version 1.0 — July 2026

This guide describes how the Limelight‑X IntelliSense Engine integrates with the Avalonia UI  
and how editor services coordinate parsing, querying, diagnostics, completions, and folding — kept strictly separate from Rust backend execution.  
It is intended for Coding Assistants and future maintainers.

This document is subordinate to `spec/cnl-editor-architecture.md` (parent authority: two-independent-parsers model, client-side-only scope) and implements the `EditorViewModel` state defined in `spec/ux/ui-viewmodels.md` §6. The services below (`ParserHost`, `QueryRunner`, `CompletionService`, `DiagnosticService`, `HoverService`, `FoldingService`, `OutlineService`) live in `/ui/intellisense`, per `CLAUDE.md` §1.

---

# 1. Editor Architecture Overview

The Limelight‑X editor is composed of:

- Avalonia UI  
- TextEditor control  
- IntelliSense Engine  
- Tree‑sitter parser host  
- Query runner  
- Rust backend  

The architecture is two independent, never-bridged paths (matching `cnl-editor-architecture.md` §2 — see that document for the canonical diagram):

```
Avalonia TextEditor                       Avalonia TextEditor
    |                                          |
    v                                          | Run/Explain (ui-viewmodels.md §6)
Editor Services Layer                          v
    |                                     PipelineService --> POST /run,/explain,/trace
    +--> ParserHost (Tree-sitter)              |            ws://.../events (spec/api.md)
    +--> QueryRunner                           v
    +--> CompletionService                Rust Backend (authoritative), via /src/api only
    +--> DiagnosticService
    +--> HoverService
    +--> FoldingService
    +--> OutlineService
```

The Editor Services Layer is never on the path to the Rust Backend, and vice versa. Each service is isolated, deterministic, and stateless except for cached results.

---

# 2. Editor Lifecycle

The editor responds to:

- Text changes  
- Cursor movement  
- Hover events  
- Folding requests  
- Outline view requests  
- Run/Execute requests  

## 2.1 OnTextChanged

```
OnTextChanged(text):
    tree = ParserHost.Parse(text)
    root = ts_tree_root_node(tree)

    completions = CompletionService.GetCompletions(text, root, cursor)
    diagnostics = DiagnosticService.GetDiagnostics(root)
    highlights = QueryRunner.Run(highlights.scm, root)
    folds = FoldingService.GetFolds(root)
    outline = OutlineService.GetOutline(root)

    UpdateEditorUI(...)
    ts_tree_delete(tree)
```

## 2.2 OnCursorMoved

```
OnCursorMoved(cursorByte):
    hover = HoverService.GetHover(root, cursorByte)
    UpdateHoverUI(hover)
```

## 2.3 OnRunRequested

```
OnRunRequested(text):
    EditorViewModel.RunCommand / ExplainCommand fires (ui-viewmodels.md §6)
    -> PipelineService sends { "source": text } to POST /trace or /explain (spec/api.md)
    -> results stream back over ws://.../events into PipelineExecutionViewModel (ui-viewmodels.md §7)
```

This is the existing, unchanged `/src/api` flow — `OnRunRequested` does not call Tree‑sitter or any editor service, and Tree‑sitter never influences Rust execution.

---

# 3. Editor Services Layer

The Editor Services Layer orchestrates all IntelliSense features.

## 3.1 ParserHost

- Loads DLL  
- Creates parser  
- Parses text  
- Frees trees  
- Frees parser on shutdown  

## 3.2 QueryRunner

- Loads `.scm` files  
- Creates queries  
- Executes queries  
- Frees queries and cursors  

## 3.3 CompletionService

- Determines grammar context  
- Suggests verbs, pronouns, templates (grammar-valid next tokens — syntactic only)  
- Suggests variables (a local scan of prior `bind_stmt` nodes — best-effort, not semantic; see §3.5)  
- Uses CST node types  

## 3.4 DiagnosticService

- Detects `ERROR` nodes  
- Detects malformed prompt holes  
- Detects unknown verbs  
- Produces non‑blocking, advisory diagnostics — never authoritative; `/explain`'s `SyntaxErrors` remain the source of truth (`cnl-editor-architecture.md` §5)  

## 3.5 HoverService

- Variable definitions (local scan of prior `bind_stmt` nodes)  
- Pronoun reference previews (local scan for the nearest preceding sentence)  
- Verb descriptions  
- Prompt hole metadata  

Variable definitions and pronoun reference previews are **syntactic, CST-only, best-effort local echoes**, not semantic resolution — they are reliable because CNL v0.1 has no branching (`spec/cnl-grammar.md` §7 Non-Goals), so "nearest preceding sentence" always agrees with what Rust's AST Normalizer would resolve. They are never authoritative and are always superseded by `/explain`'s actual response (`cnl-editor-architecture.md` §1.1.3).

## 3.6 FoldingService

- Uses fold queries  
- Produces fold regions  
- Aligns with CST boundaries  

## 3.7 OutlineService

- Extracts verbs, resources, variables  
- Produces outline entries  
- Uses CST sentence nodes  

---

# 4. Editor Integration Points

## 4.1 TextEditor Control

The TextEditor must expose:

- `Text`  
- `CursorByteOffset`  
- `OnTextChanged`  
- `OnCursorMoved`  
- `OnRunRequested`  

The IntelliSense Engine subscribes to these events.

## 4.2 UI Update Hooks

Editor services produce:

- Highlight spans  
- Diagnostic markers  
- Completion lists  
- Hover popups  
- Fold regions  
- Outline entries  

Avalonia UI consumes these via:

- `UpdateHighlights()`  
- `UpdateDiagnostics()`  
- `UpdateCompletions()`  
- `UpdateHover()`  
- `UpdateFolds()`  
- `UpdateOutline()`  

---

# 5. Integration with Tree‑sitter

## 5.1 DLL Placement

```
ui/native/tree-sitter-limelightx.dll
```

## 5.2 `.scm` Placement

```
ui/queries/highlights.scm
ui/queries/folds.scm
ui/queries/injections.scm
```

## 5.3 `.csproj` Requirements

```xml
<None Include="native\\tree-sitter-limelightx.dll" CopyToOutputDirectory="Always" />
<None Include="queries\\*.scm" CopyToOutputDirectory="Always" />
```

---

# 6. Integration with Rust Backend

The Rust backend receives **raw text**, not CST.

Rules:

- IntelliSense must never modify text  
- IntelliSense must never pre‑normalize text  
- IntelliSense must never resolve pronouns *for execution* (only Rust's AST Normalizer does that) — local, best-effort pronoun previews for hover/completion display only are permitted, per §3.5  
- IntelliSense must never generate AST or IR  

Rust backend is authoritative for:

- PEG parsing  
- AST normalization  
- pronoun resolution  
- IR generation  
- execution  

---

# 7. Memory Management Requirements

Editor services must ensure:

- Trees are freed after each parse  
- Queries are freed after each execution  
- Cursors are freed after each execution  
- Parser is freed on shutdown  

Native leaks will destabilize Avalonia.

---

# 8. Coding Assistant Integration Rules

Coding Assistants must:

- Use P/Invoke bindings exactly as documented  
- Load `.scm` files from `ui/queries/`  
- Never assume TreeSitterSharp is available  
- Never modify grammar.js without explicit instruction  
- Never generate scanner.c unless grammar requires it  
- Always treat Rust backend as authoritative  
- Always free native resources  

Coding Assistants may generate:

- Editor services  
- UI update hooks  
- Query logic  
- Completion logic  
- Diagnostics logic  
- Hover logic  
- Folding logic  
- Outline logic  

---

# 9. Summary

- The Editor Services Layer integrates Tree‑sitter with Avalonia  
- Each service is isolated and deterministic  
- Tree‑sitter provides CST + queries  
- Rust backend provides semantics  
- Memory must be managed explicitly  
- Coding Assistants must follow strict rules  

This guide defines how the IntelliSense Engine integrates with the Limelight‑X editor.
