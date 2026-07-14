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
- Error surfaces — inline parse-error squiggles come from Tree‑sitter's local error nodes (fast, client‑side, advisory only, the editor's only error state); the authoritative errors shown in the execution panel's error banner/inspectors come exclusively from an explicit Run/Explain click's `/trace`/`/explain` response — never from a background call — per `bdd-ui-interactions.md` §2.2

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

Note: the `spec/parsing/*.md` paths shown in the Tree‑sitter box above are the tier‑1 documentation copies (§3.1). At runtime the app actually loads tier 3: `ui/native/<rid>/tree-sitter-limelightx.dll` + `ui/queries/*.scm`.

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
3. **`ui/native/<rid>/tree-sitter-limelightx.dll`** (`<rid>` = `win-x64` or `win-arm64`) + **`ui/queries/{highlights,folds,injections}.scm`** — the **runtime artifacts** the shipped Avalonia app actually loads (via P/Invoke and `File.ReadAllText`, respectively). The DLLs are split per-RID (`spec/parsing/tree-sitter-build-guide.md` §0); `LimelightX.UI.csproj` resolves the matching folder automatically (the explicit publish RID when pinned, otherwise the host's own OS architecture) and copies it, flattened, into the output root — see `spec/parsing/tree-sitter-integration.md` §8. Both `win-arm64` and `win-x64` are populated (`tree-sitter-build-guide.md` §9).

Whoever edits the grammar is responsible for updating all three tiers in the same change: `spec/parsing/grammer-js.md` and the three `-scm.md` files (tier 1), `tree-sitter/grammar.js` and its three `.scm` files (tier 2, then re-run `tree-sitter generate` and rebuild the DLL), and the resulting `ui/native/`/`ui/queries/` copies (tier 3). A grammar change that touches only one tier should be treated as incomplete in review.

---

# 4. Avalonia Integration

Avalonia integrates Tree‑sitter via:

- A native Tree‑sitter runtime, embedded in the `/ui` process only (not `/src`) — approved per `CLAUDE.md` §3.5 ("Also explicitly approved for `/ui`"): the compiled `tree-sitter-limelightx.dll`, loaded via raw `[DllImport]` P/Invoke, no third-party binding package. See `spec/parsing/tree-sitter-integration.md` and `tree-sitter-build-guide.md` for the binding surface and build steps, and CLAUDE.md §3.5 for the current per-RID staging (both `ui/native/win-arm64/` and `ui/native/win-x64/` are populated)  
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
Because Tree‑sitter is error‑tolerant and reparses incrementally on every keystroke, it will often show no error node (or a different error shape) for text that `/explain`/`/trace` would still reject, and vice versa, especially mid‑edit. Tree‑sitter's error surface remains explicitly advisory (§1).

**The editor never calls the backend on its own** (`bdd-ui-interactions.md` §2.2): `EditorViewModel` has no independent `/explain` call and no `IPipelineService`/`IEventStreamService` dependency at all — an earlier "Live Validation" design that called `/explain` on a debounce timer after every keystroke has been removed entirely. `LocalDiagnostics` (Tree‑sitter, squiggle+hover, `bdd-ui-interactions.md` §2.16–§2.17) is therefore the *only* error surface in the editor while the user is typing. The backend is reached exclusively through an explicit Run or Explain click, via `RunRequested`/`ExplainRequested` → `PipelineExecutionViewModel.RunPipelineAsync`/`ExplainPipelineAsync` — and its results (including any `ERR_CNL_PARSE` disagreement with what Tree‑sitter showed) surface through `PipelineExecutionViewModel.ErrorBanner`, in the execution panel, exactly as they do for any other backend error. Nothing in the UI attempts to reconcile the two grammars' opinions against each other — they are independent, and both remaining true to that independence is the point.

This does mean the two-grammars-disagree case (a change to `spec/cnl-grammar.md`'s implementation drifting out of sync with the Tree‑sitter grammar, per the "kept in sync by hand" obligation above) is only ever discovered when the user actually clicks Run or Explain on text Tree‑sitter considered clean — there is no background check catching it earlier. That tradeoff is deliberate: catching it earlier would require either a backend call the user didn't ask for, or client-side duplication of the Rust parser's logic, both of which this architecture's "two independent parsers, never bridged" model (§2) rules out.

### Formerly: `resource`/`target`/`format_target`/`language` Lacked a Keyword‑Boundary Guard (Fixed and Verified)
**Status: fixed, rebuilt, and empirically re‑verified — see the checklist results below.** `spec/cnl-grammar.md` §5's PEG-like grammar implies these free‑text rules stop before the next keyword (`!KeywordWord`‑style negative lookahead), matching `/src/parser`'s actual (correct) behavior. `tree-sitter/grammar.js`'s implementation of the same rules previously had no such guard (`resource`/`target`/`format_target`/`language: prec.right(repeat1(/[^.\n]+/))`), and because Tree‑sitter's lexer conflict resolution prefers the *longest* match, that regex — bounded only by a literal `.` or newline — won against essentially any fixed‑literal alternative (a keyword, `pronoun`, or `name`) sharing its starting position. In practice this meant most realistic multi‑clause CNL sentences misparsed client‑side, including the canonical `Load the article from "article.txt".` example used throughout the specs, and could even drop an entire malformed sentence out of a multi‑sentence document's CST rather than isolating it as a same‑level `ERROR` node.

This was always a Tree‑sitter‑only (client‑side, advisory‑highlighting) defect — `/src/parser`'s grammar and behavior were unaffected throughout, so it never violated this section's "two independent parsers" guarantee or affected Run/Explain correctness.

**The fix**: `resource`/`target`/`format_target`/`language` now tokenize word‑by‑word via a shared hidden `_free_text_word: token(prec(-1, /[^\s.\n]+/))` instead of one greedy whole‑run regex. Since Tree‑sitter's lexer checks precedence *before* match length, every fixed keyword literal in this grammar (default precedence 0) now outranks `_free_text_word` (precedence ‑1) at any position where both could start, so free‑text rules correctly stop at the next keyword boundary. `pronoun` needed no change — empirical re‑verification confirmed it already wins at `input`'s ambiguous position (default precedence 0, same as keywords, beats `_free_text_word`'s ‑1). `name` was *initially* also left untouched on the same reasoning, but this turned out to be wrong — see the follow‑up below.

**Verified** (`tree-sitter generate` + DLL rebuild performed, then re‑ran the checklist from `spec/parsing/tree-sitter-runtime-build-guide.md` §6): all four findings resolved — `Load the article from "article.txt".` now produces a clean `load_stmt` with a correctly‑bounded `resource` node and zero `ERROR`/`MISSING` descendants; `Summarize it using {{ prompt: "..." }}.` produces a real `pronoun` node for "it" and a real `prompt_hole`; the two‑sentence document produces exactly 2 top‑level `sentence` nodes; and the completion trial‑insertion signal now distinguishes a grammar‑valid candidate (`from`, which produces a recognizable `resource` + `from` node pair) from an invalid one (`xyz`, which produces neither). `ui/tests/Intellisense/ParserHostTests.cs` and `QueryRunnerTests.cs`'s bug‑tracking tests have been inverted to assert the fixed behavior.

### Follow‑Up: `name`'s Default Precedence Truncated Multi‑Word Resources (Fixed and Verified)
**Status: fixed, rebuilt, and empirically re‑verified.** Discovered while building variable hover (`ui-intellisense-engine-spec.md` §7.1), which needed exactly the sentence shape that turned out to be broken: `Let article be the text from "article.txt".` — `bind_stmt`'s `choice($.resource_from, $.expression)` position. `name`'s default precedence (0, same as keyword literals) meant it beat `_free_text_word` (‑1) for the *first* word of any multi‑word resource sharing that position, so `resource_from`'s resource got truncated to one word (`"the"`) and everything after became an unparsed `ERROR` — the sentence never reduced to a valid `bind_stmt` at all. The same defect independently broke `input`'s `choice($.resource, $.name, $.pronoun)`: `Extract the entities from the article.` — `cnl-grammar.md` §2.2's own canonical example — truncated `"the article"` to `"the"` the same way.

**The fix**: `name` is now `token(prec(-2, /[A-Za-z_][A-Za-z0-9_]*/))` — one precedence level below `_free_text_word` (‑1). At the two ambiguous positions (`resource_from` vs `expression`, and `input`'s three‑way choice), the lexer now prefers continuing as a free‑text word over reducing to a standalone name, so multi‑word resources/targets are never truncated regardless of whether their first word happens to be identifier‑shaped. `name` in `bind_stmt`'s unambiguous first slot (`"Let" $.name "be"`) is unaffected — no competing alternative exists there.

**Accepted narrowing**: a single bare name used as a *complete* expression with nothing following (e.g. `Let summary be article.` — no `from`, not a pronoun) no longer parses, since the lexer commits to a free‑text‑word token before the parser can know `resource_from` will dead‑end reaching for a `from` that isn't there. No example anywhere in the specs uses this exact shape (§2.5's own example uses a pronoun phrase, `"the result"`, not a bare name), so this is judged an acceptable, narrower tradeoff against the much more common multi‑word‑truncation bug it replaces. Variable hover (§7.1) is designed to not depend on this distinction — it matches on node *text* against known bindings for both `resource`‑ and `name`‑typed nodes, not on the node being specifically `name`‑typed.

Full empirical write‑up for both rounds (discovery, escalations, and the specific test cases that surfaced each one) lives in `spec/parsing/tree-sitter-runtime-build-guide.md` §6 — that is the single source of truth for this issue's details, now including both fixes' descriptions and re‑verification checklists; this entry is the pointer other docs/code should link to, not a duplicate narrative.

---

# 6. External File Specifications

All Tree‑sitter files are external and located in:

```
spec/parsing/
```

They are not embedded in this document.  
Coding Assistants must read them from disk when generating editor features.

This is tier 1 of the three-tier file model (§3.1) — the documentation copies. Runtime code loads tier 3 (`ui/native/<rid>/tree-sitter-limelightx.dll`, `ui/queries/*.scm`) instead; do not point runtime code at `spec/parsing/`.

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
