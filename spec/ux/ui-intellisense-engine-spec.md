# Limelight‑X IntelliSense Engine Specification  
## Version 1.0 — July 2026

This document defines the architecture, responsibilities, data flow, and behavioral rules  
for the **Limelight‑X IntelliSense Engine**.  
It is intended for Coding Assistants and future maintainers of the Limelight‑X CNL editor.

The IntelliSense Engine is built on top of the native Tree‑sitter grammar DLL  
(`tree-sitter-limelightx.dll`) and provides grammar‑aware editor features  
without participating in compilation or execution.

This document is subordinate to `spec/cnl-editor-architecture.md` (parent authority: two-independent-parsers model). §5 and §7 below describe features that sit at the syntactic/semantic boundary that document's §1.1.3 draws — read that section first.

---

# 1. Purpose

The IntelliSense Engine provides real‑time, grammar‑aware assistance to users writing  
Limelight‑X Constrained Natural Language (CNL).  
Its goals are:

- Improve correctness of CNL input  
- Reduce cognitive load  
- Provide structural awareness  
- Surface errors early  
- Enable rich editor UX  

The engine is **editor‑only** and must never influence the Rust backend’s deterministic  
PEG parser or IR compiler.

---

# 2. Core Responsibilities

The IntelliSense Engine is responsible for:

## 2.1 Grammar‑Aware Completions
- Suggest verbs, keywords, pronouns, variables, and prompt templates  
- Use Tree‑sitter CST context to determine valid next tokens  
- Provide ranked suggestions based on grammar position  

## 2.2 Live Diagnostics
- Detect Tree‑sitter `ERROR` nodes  
- Surface missing punctuation, malformed prompt holes, invalid identifiers  
- Provide non‑blocking diagnostics (Rust backend remains authoritative)  

## 2.3 Hover Information
- Show variable definitions  
- Show pronoun reference previews  
- Show verb descriptions  
- Show sentence summaries  

## 2.4 Structural Navigation
- Select entire sentences  
- Navigate between sentences  
- Select resource phrases  
- Select prompt holes  
- Select variable bindings  

## 2.5 Folding and Outline
- Provide fold regions per sentence  
- Generate outline view from CST  
- Extract verbs, resources, and variable names  

## 2.6 Query Execution
- Run highlight, fold, and injection queries  
- Process matches into editor decorations  

---

# 3. Architecture Overview

Two independent, never-bridged paths — not a pipeline (matches `cnl-editor-architecture.md` §2; see that document for the canonical diagram):

```
Avalonia TextEditor                     Avalonia TextEditor
    |                                        |
    v                                        | Run/Explain (ui-viewmodels.md §6)
IntelliSense Engine                          v
    |                                   /src/api (Rust, HTTP+WS, spec/api.md)
    +--> tree-sitter-limelightx.dll          |
            - CST generation                 v
            - error nodes                Rust Backend (authoritative)
            - query engine                   - PEG parser
                                              - AST normalizer
                                              - pronoun resolver
                                              - IR compiler
                                              - execution
```

The IntelliSense Engine is never on the path to the Rust Backend — it does not call `/src/api` and is not called by it. Tree‑sitter is used for **editor semantics**.  
Rust is used for **execution semantics**, reached only via `/src/api`.

---

# 4. Data Flow

## 4.1 Editor → IntelliSense

```
OnTextChanged(text):
    tree = ts_parser_parse_string(...)
    root = ts_tree_root_node(tree)
    completions = ComputeCompletions(root, cursor)
    diagnostics = ComputeDiagnostics(root)
    hovers = ComputeHover(root, cursor)
    folds = ComputeFolds(root)
    outline = ComputeOutline(root)
    UpdateEditorUI(...)
```

## 4.2 Editor → Rust Backend

```
OnRunRequested(text):
    EditorViewModel.RunCommand/ExplainCommand -> POST /trace or /explain (spec/api.md)
    Rust parses CNL deterministically, results stream back over ws://.../events
    display streamed diagnostics/output in the inspector panels (ui-viewmodels.md §7)
```

This is the existing, unchanged `/src/api` flow (§3) — it does not go through the IntelliSense Engine. IntelliSense diagnostics (local, advisory) and backend diagnostics (`SyntaxErrors`, authoritative) are **complementary, not interchangeable** — see `cnl-editor-architecture.md` §5's "Tree‑sitter's view of 'valid' can disagree with Rust's."

---

# 5. Completion Engine Specification

## 5.1 Completion Sources

- Verbs (Load, Extract, Summarize, Translate, Rewrite, Format, Let) — syntactic, grammar-valid-next-token only  
- Keywords (from, using, as, to, be) — syntactic, grammar-valid-next-token only  
- Pronouns (it, them, the result, the output, this, that) — syntactic, grammar-valid-next-token only  
- Variables (from `bind_stmt` nodes) — **syntactic, CST-only, best-effort**: a local scan of prior `bind_stmt` nodes in the same document, not a semantic lookup. See §7.2's note — the same boundary applies here.  
- Prompt templates (`{{ prompt: "" }}` patterns) — syntactic, structural skeleton only, no content suggestions

## 5.2 Context Rules

### Sentence Start
Suggest verbs.

### After `Let <name> be`
Suggest resource expressions or prompt holes.

### Inside resource position
Suggest variables and pronouns.

### Inside prompt hole
Suggest prompt templates.

## 5.3 Ranking Rules

- Grammar‑valid tokens rank highest  
- Variables rank above pronouns  
- Verbs rank above keywords  
- Prompt templates rank lowest  

---

# 6. Diagnostics Specification

Diagnostics are derived from:

- Tree‑sitter `ERROR` nodes  
- Missing nodes (e.g., missing string literal)  
- Invalid identifiers  
- Malformed prompt holes  
- Unknown verbs  

Diagnostics must:

- Never block Rust execution  
- Never override Rust diagnostics  
- Be displayed immediately  
- Be cleared when errors resolve  

---

# 7. Hover Engine Specification

## 7.1 Variable Hover
Show the binding sentence:
```
Let article be the text from "article.txt".
```

## 7.2 Pronoun Hover
Show best‑effort reference preview:
```
Pronoun refers to: SummarizeStmt at line 3
```

**§7.1 and §7.2 are syntactic, CST-only, best-effort local echoes — not semantic resolution.** Both are computed by a local scan (nearest preceding `bind_stmt` for §7.1, nearest preceding sentence for §7.2), never by calling Rust or replicating the AST Normalizer's logic. They are reliable in practice only because CNL v0.1 has no branching (`spec/cnl-grammar.md` §7 Non-Goals: no conditionals/loops) — "nearest preceding sentence" always agrees with what Rust's normalizer would actually resolve. They must never be presented as, or relied upon as, authoritative: `/explain`'s streamed response is always the source of truth for what a pronoun or variable actually resolves to (`cnl-editor-architecture.md` §1.1.3).

## 7.3 Verb Hover
Show short description:
```
Summarize: Reduce text to a shorter form.
```

## 7.4 Prompt Hole Hover
Show template description.

---

# 8. Structural Navigation Specification

## 8.1 Sentence Selection
Use CST `sentence` node boundaries.

## 8.2 Resource Selection
Use `resource` node boundaries.

## 8.3 Prompt Hole Selection
Use `prompt_hole` node boundaries.

## 8.4 Variable Binding Selection
Use `bind_stmt` node boundaries.

---

# 9. Folding Specification

Folding is driven by `.scm` fold queries.

Rules:
- Each `sentence` is foldable  
- Prompt holes may be foldable  
- Fold regions must align with CST boundaries  
- Folding must not hide diagnostics  

---

# 10. Outline Specification

Outline entries include:

- Verb  
- Resource  
- Variable name (for `Let`)  
- Line number  

Example:
```
1. Load "article.txt"
2. Extract entities from article
3. Summarize it
4. Let summary be the result
```

---

# 11. Query Engine Specification

The IntelliSense Engine must:

- Load `.scm` files  
- Create queries  
- Create cursors  
- Execute queries  
- Process matches  
- Free queries and cursors  

Queries must be recreated each cycle.

---

# 12. Memory Management Rules

- Free trees after each parse  
- Free queries after each execution  
- Free cursors after each execution  
- Free parser on shutdown  

Memory leaks in native code will destabilize Avalonia.

---

# 13. Coding Assistant Rules

Coding Assistants must:

- Use P/Invoke bindings exactly as documented  
- Never assume TreeSitterSharp is available  
- Never modify grammar.js without explicit instruction  
- Never generate scanner.c unless grammar requires it  
- Always free native resources  
- Always treat Rust backend as authoritative  

Coding Assistants may generate:

- Completion logic  
- Diagnostics logic  
- Hover logic  
- Folding logic  
- Outline logic  
- Query runners  
- Editor services  

---

# 14. Summary

- IntelliSense is powered by Tree‑sitter CST + queries  
- Rust backend is authoritative for semantics  
- IntelliSense provides grammar‑aware UX  
- Memory must be managed explicitly  
- Coding Assistants must follow strict integration rules  

This specification defines the complete IntelliSense architecture for Limelight‑X.
