# Limelight‑X Intellisense Architecture
## Version 1.0 — July 2026

This document defines how Coding Assistants must operate when generating code, documentation,  
grammar rules, IntelliSense logic, or editor integrations for the Limelight‑X CNL system.  
It ensures deterministic behavior, architectural consistency, and safe extension of the platform.

This document is subordinate to `spec/cnl-editor-architecture.md` (parent authority) and `CLAUDE.md` (repository-wide rules) wherever either would otherwise conflict with something below.

---

# 1. Purpose of This Document

Coding Assistants are used to accelerate development of Limelight‑X by generating:

- C# editor integration code  
- Tree‑sitter query logic  
- IntelliSense features  
- Documentation  
- Grammar‑aligned utilities  
- Rust backend scaffolding (when requested)  

This document defines the **rules**, **constraints**, and **allowed behaviors** for Coding Assistants  
working within the Limelight‑X architecture.

---

# 2. Core Principles

Coding Assistants must follow these foundational principles:

## 2.1 Determinism
- All generated code must be deterministic.  
- No probabilistic behavior.  
- No “creative” grammar changes unless explicitly requested.

## 2.2 Single Source of Truth
- The Rust backend is the authoritative compiler.  
- Tree‑sitter is editor‑only.  
- Grammar.js must match the PEG grammar exactly.

## 2.3 Safety
- Assistants must not introduce new verbs, keywords, or pronouns.  
- Assistants must not modify grammar.js without explicit instruction.  
- Assistants must not generate scanner.c unless grammar requires it.

## 2.4 Memory Discipline
- All native Tree‑sitter resources must be freed.  
- No leaks in parser, tree, query, or cursor lifecycles.

## 2.5 Non‑Interference
- IntelliSense must never override Rust diagnostics.  
- IntelliSense must never change execution semantics.

---

# 3. Allowed Tasks

Coding Assistants may generate:

## 3.1 Editor‑Side Features
- Completion logic  
- Diagnostics logic  
- Hover logic  
- Folding logic  
- Outline logic  
- Query runners  
- Syntax highlighting  
- Structural navigation  

## 3.2 Documentation
- Architecture documents  
- Integration guides  
- Grammar reference docs  
- IntelliSense specifications  
- Build instructions  

## 3.3 Rust Backend Utilities (when requested)
- PEG grammar helpers  
- AST utilities  
- IR scaffolding  
- Execution pipeline helpers  

---

# 4. Forbidden Tasks

Coding Assistants must **never**:

- Modify grammar.js without explicit user instruction  
- Invent new CNL syntax  
- Generate scanner.c unless grammar requires it  
- Change exported DLL symbols  
- Introduce nondeterministic behavior  
- Override Rust backend semantics  
- Produce CST → AST transformations that conflict with Rust  
- Generate TreeSitterSharp code, or use any other third-party Tree‑sitter binding package (not approved — `CLAUDE.md` §3.5)  
- Depend on any Tree‑sitter runtime other than the self-contained `tree-sitter-limelightx.dll` (e.g. a separate shared `tree-sitter.dll` runtime, or the tree-sitter-cli's own runtime) — `tree-sitter-limelightx.dll` itself is approved and required, not forbidden; see `spec/parsing/tree-sitter-integration.md` §3  

---

# 5. Tree‑sitter Integration Rules

Coding Assistants must follow the official integration architecture:

## 5.1 DLL Loading
- Use P/Invoke to load `tree-sitter-limelightx.dll`  
- Use the exported symbol:  
  ```
  const TSLanguage * tree_sitter_limelightx();
  ```

## 5.2 Parser Lifecycle
- Create parser via `ts_parser_new()`  
- Set language via `ts_parser_set_language()`  
- Parse text via `ts_parser_parse_string()`  
- Free parser on shutdown  

## 5.3 Tree Lifecycle
- Free trees after each parse  
- Never reuse trees across documents  

## 5.4 Query Lifecycle
- Load `.scm` files  
- Create queries  
- Create cursors  
- Execute queries  
- Free queries and cursors  

---

# 6. IntelliSense Integration Rules

Coding Assistants must implement IntelliSense using:

- CST context  
- Query matches  
- Node types  
- Error nodes  
- Grammar position rules  

Assistants must **not**:

- Infer semantics beyond CST  
- Perform *authoritative* pronoun resolution (only Rust's AST Normalizer does that) — a local, best-effort, CST-only preview for hover/completion display is permitted (`spec/cnl-editor-architecture.md` §1.1.3), but must never be treated as, or substituted for, Rust's resolution  
- Modify execution semantics  

---

# 7. Grammar Alignment Rules

Grammar.js and the PEG grammar must remain aligned.

Coding Assistants must:

- Preserve rule names  
- Preserve sentence structure  
- Preserve keyword sets  
- Preserve pronoun sets  
- Preserve verb sets  

Assistants may generate:

- Grammar documentation  
- Grammar visualizations  
- Grammar test cases  

Assistants may not generate:

- New grammar constructs  
- New verbs or keywords  
- New pronouns  
- New sentence patterns  

unless explicitly instructed.

---

# 8. Rust Backend Interaction Rules

Coding Assistants must treat Rust as authoritative.

Rules:

- Rust receives raw text  
- Rust performs deterministic parsing  
- Rust resolves pronouns  
- Rust generates IR  
- Rust executes workflows  

IntelliSense must never:

- Change Rust input  
- Pre‑normalize text  
- Pre‑resolve pronouns  
- Modify AST semantics  

---

# 9. Memory Management Requirements

Coding Assistants must ensure:

- `ts_tree_delete()` is called  
- `ts_query_delete()` is called  
- `ts_query_cursor_delete()` is called  
- `ts_parser_delete()` is called  

Memory leaks in native code will destabilize Avalonia.

---

# 10. File and Folder Conventions

Coding Assistants must follow (per `CLAUDE.md` §1's `/ui` layout):

```
ui/native/win-arm64/tree-sitter-limelightx.dll
ui/native/win-x64/tree-sitter-limelightx.dll
ui/queries/highlights.scm
ui/queries/folds.scm
ui/queries/injections.scm
ui/intellisense/          (ParserHost, QueryRunner, CompletionService,
                            DiagnosticService, HoverService, FoldingService,
                            OutlineService — spec/ux/ui-intellisense-implementation-guide.md §1)
```

There is no `ui/editor/` directory — everything editor-service-related lives in `ui/intellisense/` above; the existing `ui/components/CnlEditor.axaml(.cs)` remains the TextEditor control itself and is unaffected by this layout.

DLLs must be copied via `.csproj`:

```xml
<None Include="native\\tree-sitter-limelightx.dll" CopyToOutputDirectory="Always" />
```

---

# 11. Coding Style Requirements

Coding Assistants must:

- Avoid reflection  
- Avoid dynamic code generation  
- Avoid global mutable state  
- Use explicit types  
- Use clear naming aligned with grammar  

---

# 12. Testing Requirements

Coding Assistants must generate:

- CST tests  
- Query tests  
- IntelliSense tests  
- Folding tests  
- Highlighting tests  

Tests must be deterministic and reproducible.

**CI gating:** `tree-sitter-limelightx.dll` is currently built for ARM64 only (`spec/parsing/tree-sitter-build-guide.md` §0). Any test that P/Invokes the DLL will fail with `BadImageFormatException` on the `windows-latest` (x64) CI runner and must be skipped/gated there until the deferred `win-x64` build exists (`CLAUDE.md` §3.5).

---

# 13. Versioning Rules

When grammar or `.scm` files change:

- Increment DLL version  
- Update P/Invoke bindings  
- Update IntelliSense logic  
- Update documentation  

---

# 14. Summary

- Coding Assistants accelerate development  
- They must follow deterministic rules  
- They must treat Rust as authoritative  
- They must use Tree‑sitter for editor semantics  
- They must manage memory explicitly  
- They must not modify grammar without instruction  
- They must generate safe, aligned, deterministic code  

This onboarding document is the canonical reference for Coding Assistants working on Limelight‑X.
