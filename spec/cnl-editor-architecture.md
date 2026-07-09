# Limelight‑X CNL Editor Architecture  
## Tree‑sitter Integration Specification  
Version: v0.1  
Status: Authoritative

This document defines the architecture for integrating **Tree‑sitter** into the **Avalonia‑based Limelight‑X CNL Editor**.  
It is intended for Coding Assistants that generate code, scaffolding, or editor features.

Tree‑sitter is a **client‑side‑only, editor‑UX concern**. It never talks to the Rust backend and never participates in validation or execution. The Rust pipeline (`/src/parser` and onward) remains the sole source of truth for CNL semantics, reached exclusively through the existing `/src/api` HTTP + WebSocket contract (`spec/api.md`), exactly as it is today. See §5 for the resulting two‑parser model and how the two are kept from being confused with one another.

The architecture consists of:

1. A **Tree‑sitter grammar** (`spec/parsing/grammer-js.md`)  
2. A **highlight query file** (`spec/parsing/highlights-scm.md`)  
3. A **language injection file** (`spec/parsing/injections-scm.md`)  
4. A **folding query file** (`spec/parsing/folds-scm.md`)  
5. The **PEG grammar reference** (`spec/parsing/peg-grammar.md`)  
6. The **Avalonia integration layer** (client‑side only)

This document is the authoritative specification for how these components interact, subordinate to `spec/ux/ui-architecture.md` §6/§11 (no direct Rust integration) and `CLAUDE.md` §3.5 (approved dependencies) wherever the two would otherwise conflict.

---

# 1. Overview

The Limelight‑X CNL editor uses **Tree‑sitter** as its incremental parsing engine.  
Tree‑sitter provides:

- Deterministic PEG parsing  
- Incremental syntax tree updates  
- Error nodes  
- Structural selection  
- Syntax highlighting  
- Grammar‑aware completions  
- Folding regions  
- Injection support for embedded prompts

Avalonia provides:

- Text editor UI  
- Styling  
- Hover tooltips (from Tree‑sitter's local CST — see §5)  
- Completion UI (from Tree‑sitter's local CST — see §5)  
- Panels for AST, IR, and execution output — **unrelated to Tree‑sitter**: these remain driven entirely by `/src/api`'s streamed JSON events (`raw_ast_generated`, `normalized_ast_generated`, `ir_generated`, ...) exactly as specified in `ui-architecture.md` §6–§7, `ui-components.md` §5, and `spec/api.md`  
- Error surfaces — inline parse-error squiggles come from Tree‑sitter's local error nodes (fast, client‑side, advisory only); the authoritative `SyntaxErrors` shown in the error banner/inspectors still come exclusively from `/explain`, per `bdd-ui-interactions.md` §2.2 (unchanged)

The Rust backend provides, exactly as it does today and reached only via `/src/api` (`spec/api.md`) — Tree‑sitter has no part in this path:

- Parsing (`/src/parser`, its own PEG implementation — independent of Tree‑sitter's copy)  
- AST normalization  
- Pronoun resolution  
- IR generation  
- Execution engine

---

# 1.1 PEG Grammar Reference

The Limelight‑X CNL syntax is defined by a formal **Parsing Expression Grammar (PEG)**.  
This PEG grammar is the authoritative specification for:

- all sentence patterns  
- all lexical rules  
- all keyword boundaries  
- all resource and target parsing behavior  
- all expression hole syntax  
- all pronoun resolution inputs  
- all AST node mappings

Tree‑sitter grammars are PEG‑like and map directly to this specification.  
The `spec/parsing/grammer-js.md` file is a **Tree‑sitter implementation** of the PEG grammar.

`spec/cnl-grammar.md` remains "the authoritative contract for the parser" (its own §Purpose) for `/src/parser`'s Rust implementation. `spec/parsing/peg-grammar.md` is a formal restatement of that same grammar, scoped to driving the Tree‑sitter grammar and other editor tooling — the two must never diverge in content, and `spec/cnl-grammar.md` wins if they ever do. Nothing in this document licenses `/src/parser` to be re-derived from or replaced by the PEG file.

## 1.1.1 PEG Grammar Location

The full PEG grammar for Limelight‑X is documented in:

```
spec/parsing/peg-grammar.md
```

This file contains the complete PEG grammar expressed in Markdown as a code snippet.  
It is referenced, at development/generation time (not at program runtime), by:

- the Tree‑sitter grammar authoring process  
- Coding Assistants generating editor features

It is not read by `/src/parser`, the AST normalizer, or the IR compiler at runtime — those are governed by `spec/cnl-grammar.md` and implemented independently in Rust, per `CLAUDE.md` §2.1–§2.3.

## 1.1.2 PEG Grammar → Tree‑sitter Mapping

Tree‑sitter grammars are deterministic PEG parsers.  
The mapping rules are:

- PEG `Sequence` → Tree‑sitter `seq(...)`  
- PEG `OrderedChoice` → Tree‑sitter `choice(...)`  
- PEG `ZeroOrMore` → Tree‑sitter `repeat(...)`  
- PEG `OneOrMore` → Tree‑sitter `repeat1(...)`  
- PEG `Optional` → Tree‑sitter `optional(...)`  
- PEG `Literal` → Tree‑sitter string literal  
- PEG `Regex` → Tree‑sitter `/regex/`  
- PEG `Nonterminal` → Tree‑sitter rule reference

All Tree‑sitter rules in `spec/parsing/grammer-js.md` must correspond exactly to the PEG grammar definitions.

## 1.1.3 PEG Grammar Guarantees

The PEG grammar guarantees:

- deterministic parsing  
- no ambiguity  
- no precedence rules  
- no left recursion  
- strict sentence boundaries  
- strict keyword boundaries  
- strict expression hole syntax  
- strict pronoun definitions  
- strict AST node mapping

These guarantees allow the editor to provide, entirely client‑side and without a backend round‑trip:

- grammar‑aware completions  
- grammar‑aware local error surfaces (advisory only — see §1)  
- grammar‑aware folding  
- grammar‑aware syntax highlighting  
- grammar‑aware structural selection

Grammar‑aware completions and hover are necessarily syntactic only — bounded to what the CST shape says is valid next (a keyword, a pronoun, an expression‑hole skeleton). They cannot offer semantic suggestions (e.g. resource names actually bound earlier in the file, or a pronoun's resolved target) because that requires the AST Normalizer, which runs only in Rust and only reaches the UI via `/explain`'s streamed events, not via Tree‑sitter. IR previews are explicitly **not** a Tree‑sitter capability — the IR panel is, and remains, driven solely by `/src/api`'s `ir_generated` event (`ui-components.md` §5.4).

## 1.1.4 Coding Assistant Requirements

Coding Assistants must:

- reference the PEG grammar when generating Tree‑sitter rules  
- reference the PEG grammar when generating AST builders  
- reference the PEG grammar when generating IR compilers  
- reference the PEG grammar when generating editor behaviors  
- never introduce syntax not present in the PEG grammar  
- never infer unsupported sentence patterns  
- never relax keyword boundaries  
- never relax expression hole syntax

The PEG grammar is the authoritative contract for Limelight‑X parsing.

---

# 2. Architecture Diagram

Two independent parsers exist side by side. They are never bridged to each other — see §5.

```
+---------------------------+        +----------------------------+
|       Avalonia UI         |        |       Avalonia UI          |
|  - TextEditor              |        |  - AST / IR / Result Panels |
|  - Hover / Completion UI   |        |  - Error Banner              |
|  - Local error squiggles   |        +--------------+---------------+
+-------------+---------------+                       |
              |                                        | POST /run,/explain,/trace
              v                                        | ws://127.0.0.1:<port>/events
+---------------------------+                          v
|       Tree-sitter          |             +----------------------------+
|  (in-process, client-only) |             |   /src/api (Rust, HTTP+WS)  |
|  - spec/parsing/grammer-js.md |          |   unchanged, per spec/api.md |
|  - spec/parsing/highlights-scm.md |      +--------------+---------------+
|  - spec/parsing/injections-scm.md |                     |
|  - spec/parsing/folds-scm.md |                          v
+---------------------------+             +----------------------------+
                                            |  /src/parser (Rust, own PEG) |
                                            |  -> normalizer -> IR -> eval |
                                            +----------------------------+
```

Tree‑sitter's tree feeds only the left‑hand column (highlighting, folding, hover, completion, structural selection, local error squiggles). It never reaches, and is never derived from, the Rust pipeline on the right — that flow is `ui-architecture.md` §6, unchanged by this document.

Note: the `spec/parsing/*.md` paths shown in the Tree‑sitter box above are the tier‑1 documentation copies (§3.1). At runtime the app actually loads tier 3: `ui/native/tree-sitter-limelightx.dll` + `ui/queries/*.scm`.

---

# 3. Required Files

The editor requires four Tree‑sitter files, all located in:

```
spec/parsing/
```

### 1. `spec/parsing/grammer-js.md`  
Tree‑sitter grammar implementing the PEG grammar.

### 2. `spec/parsing/highlights-scm.md`  
Syntax highlighting scopes.

### 3. `spec/parsing/injections-scm.md`  
Language injection for expression holes (`{{ prompt: "..." }}`).

### 4. `spec/parsing/folds-scm.md`  
Folding regions (one fold per CNL sentence).

These files are external artifacts and not embedded in this document. (Note: `grammer-js.md` is the actual on-disk filename — a pre-existing typo for "grammar-js.md"; kept as-is here to match disk rather than silently diverging from the file Coding Assistants will actually read. Renaming it is optional cleanup, not required by this spec.)

## 3.1 Three‑Tier File Model

The grammar and its query files exist in three places on disk, each with a distinct purpose. They must be kept in sync by hand (§5) — there is no build step that regenerates one from another today:

1. **`spec/parsing/*.md`** (`grammer-js.md`, `highlights-scm.md`, `folds-scm.md`, `injections-scm.md`, `peg-grammar.md`) — the **documentation / source-of-truth copies**. Read by Coding Assistants at development/generation time (§1.1.1). Not read by the running app.
2. **`tree-sitter/`** (repo root) — the **build scaffold**: `grammar.js` and the three `.scm` files, plus the tree-sitter-cli-generated `package.json`, `tree-sitter.json`, and (after `tree-sitter generate`) `src/parser.c`, `src/grammar.json`, `src/node-types.json`. This is what `tree-sitter generate` and MSVC (`cl /LD ...`) actually consume to produce the DLL — see `spec/parsing/tree-sitter-build-guide.md`.
3. **`ui/native/tree-sitter-limelightx.dll`** + **`ui/queries/{highlights,folds,injections}.scm`** — the **runtime artifacts** the shipped Avalonia app actually loads (via P/Invoke and `File.ReadAllText`, respectively). These are build outputs/copies of tier 2, wired into `LimelightX.UI.csproj` (see `spec/parsing/tree-sitter-integration.md` §8).

Whoever edits the grammar is responsible for updating all three tiers in the same change: `spec/parsing/grammer-js.md` and the three `-scm.md` files (tier 1), `tree-sitter/grammar.js` and its three `.scm` files (tier 2, then re-run `tree-sitter generate` and rebuild the DLL), and the resulting `ui/native/`/`ui/queries/` copies (tier 3). A grammar change that touches only one tier should be treated as incomplete in review.

---

# 4. Avalonia Integration

Avalonia integrates Tree‑sitter via:

- A native Tree‑sitter runtime, embedded in the `/ui` process only (not `/src`) — approved per `CLAUDE.md` §3.5 ("Also explicitly approved for `/ui`"): the compiled `tree-sitter-limelightx.dll`, loaded via raw `[DllImport]` P/Invoke, no third-party binding package. See `spec/parsing/tree-sitter-integration.md` and `tree-sitter-build-guide.md` for the binding surface and build steps, and CLAUDE.md §3.5 for the current ARM64-only staging (a `win-x64` build is separate, deferred work)  
- A text‑change event pipeline  
- A syntax tree update loop  
- A highlight mapping layer  
- A completion provider  
- A hover provider  
- A local error-node surface provider (advisory squiggles only, per §1)

This pipeline is entirely local to the Avalonia process. It does not call `/src/api` and is not called by it.

### Text Change Pipeline

```
OnTextChanged(text):
    tree = parser.parse(text)                          // incremental, in-process
    highlights = query(spec/parsing/highlights-scm.md, tree)
    folds = query(spec/parsing/folds-scm.md, tree)
    injections = query(spec/parsing/injections-scm.md, tree)
    errorNodes = collectErrorNodes(tree)                // advisory only, see §1
    updateEditorDecorations(tree, highlights, folds, injections, errorNodes)
```

Note what this pipeline deliberately does **not** do: it does not build a `raw_ast`/`normalized_ast`/IR payload, and it does not call any backend. Those remain `/explain`'s and `/trace`'s job, driven by `EditorViewModel`'s existing debounce-triggered call and by `PipelineExecutionViewModel`, exactly as specified today (`ui-viewmodels.md` §6, `bdd-ui-interactions.md` §2.2, §3).

---

# 5. Relationship to the Rust Backend

Tree‑sitter and the Rust backend are **two independent parsers of the same grammar, never bridged**:

- Tree‑sitter parses client‑side, in‑process, on every keystroke, for editor decoration only (highlighting, folding, injections, structural selection, completions, hover, advisory local error squiggles).
- `/src/parser` (Rust) parses server‑side, exactly as it does today, reached only through `/src/api`'s `POST /explain` / `POST /trace` and the `ws://127.0.0.1:<port>/events` stream (`spec/api.md`). It alone produces the raw AST, normalized AST, IR, and execution results shown in the inspector panels, and it alone determines `SyntaxErrors` / the authoritative validation state.
- The UI never sends a Tree‑sitter CST, or anything derived from it, to `/src/api` or to Rust by any other channel (FFI, UniFFI, or otherwise). Doing so would violate `ui-architecture.md` §6 ("UI must not call Rust directly") and §11's "direct Rust integration" Non‑Goal, and no such channel is approved by `CLAUDE.md` §3.5.
- The UI continues to send the same `{ "source": "<CNL text>" }` request body it sends today for `/explain`/`/trace` — plain text, not a tree.

### Consequence: two grammars must be kept in sync by hand
Because both parsers implement the same grammar independently, they can drift: a change to `spec/cnl-grammar.md` (and its `/src/parser` implementation) is not automatically reflected in `spec/parsing/peg-grammar.md` or the Tree‑sitter grammar files. There is no build-time or runtime check that enforces agreement between them. Whoever changes `spec/cnl-grammar.md` is responsible for updating `spec/parsing/peg-grammar.md` and the Tree‑sitter grammar/query files in the same change; reviewers should treat a CNL grammar change without a matching Tree‑sitter grammar change as incomplete.

### Consequence: Tree‑sitter's view of "valid" can disagree with Rust's
Because Tree‑sitter is error‑tolerant and reparses incrementally on every keystroke, it will often show no error node (or a different error shape) for text that `/explain` will still reject, and vice versa, especially mid‑edit. This is expected and acceptable **only** because Tree‑sitter's error surface is explicitly advisory (§1) — it must never suppress, delay, or replace the `/explain` round‑trip that populates `SyntaxErrors`, and it must never be treated as authoritative for whether the source is valid to Run/Explain.

---

# 6. External File Specifications

All Tree‑sitter files are external and located in:

```
spec/parsing/
```

They are not embedded in this document.  
Coding Assistants must read them from disk when generating editor features.

This is tier 1 of the three-tier file model (§3.1) — the documentation copies. Runtime code loads tier 3 (`ui/native/tree-sitter-limelightx.dll`, `ui/queries/*.scm`) instead; do not point runtime code at `spec/parsing/`.

---

# 7. Summary

This document defines:

- The architecture for integrating Tree‑sitter into the Avalonia Limelight‑X editor, as a **client‑side‑only editor‑UX concern**  
- The PEG grammar reference and mapping rules  
- The external Tree‑sitter files in `spec/parsing/`  
- The Avalonia ⇄ Tree‑sitter loop (local decoration) kept strictly separate from the unchanged Avalonia ⇄ `/src/api` ⇄ Rust pipeline (validation/execution)  
- The authoritative grammar and query specifications, and the hand‑sync obligation between `spec/cnl-grammar.md` and the Tree‑sitter grammar files (§5)

Coding Assistants should use this document as the canonical reference when generating:

- Editor scaffolding  
- Syntax highlighting, folding, and injections  
- Completion and hover logic (syntactic only — see §1.1.3)  
- Local, advisory error-node surfacing

Coding Assistants must **not** use this document to justify any new Rust-facing integration path, AST/IR construction from the Tree‑sitter CST, or new approved dependency beyond what `CLAUDE.md` §3.5 already lists — those remain out of scope here.
