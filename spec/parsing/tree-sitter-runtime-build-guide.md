# Limelight‑X Tree‑sitter Runtime DLL Build Guide

This document provides step‑by‑step instructions for building `tree-sitter-runtime.dll` — the actual Tree‑sitter **parsing engine** (`ts_parser_*`, `ts_tree_*`, `ts_node_*`, `ts_query_*`), as distinct from `tree-sitter-limelightx.dll` (the CNL **grammar**, built per `spec/parsing/tree-sitter-build-guide.md`). See `spec/cnl-editor-architecture.md` §3.1 for how these two DLLs fit into the three‑tier file model, and §5 below for why both are required.

---

## 0. Why This DLL Is Necessary (Corrects a Claim in `tree-sitter-integration.md`)

`spec/parsing/tree-sitter-integration.md` (and the original build guide) describe `tree-sitter-limelightx.dll` as self‑contained — "the full Tree‑sitter runtime, statically linked." **This is incorrect, discovered empirically**: `dumpbin /exports` on the built grammar DLL shows it exports exactly one symbol, `tree_sitter_limelightx`. None of `ts_parser_new`, `ts_node_child`, `ts_query_new`, etc. exist in it.

This is how Tree‑sitter's C API is actually designed: a grammar's generated `parser.c` only contains the grammar's own state tables and the `tree_sitter_<name>()` accessor that returns a `const TSLanguage*`. The parsing/tree/node/query **engine** that consumes that `TSLanguage*` lives in Tree‑sitter's core library (`tree-sitter/tree-sitter` on GitHub, `lib/` directory) — a completely different codebase from anything vendored in this repo's `tree-sitter/` folder (which only has the grammar‑specific `parser.c` + the minimal headers needed to compile it, per `tree-sitter-build-guide.md`).

Confirmed by inspecting the reference C# binding project [Summpot/TreeSitterSharp](https://github.com/Summpot/TreeSitterSharp): it vendors Tree‑sitter core (`tree-sitter/tree-sitter`) as a git submodule at `native/sources/tree-sitter/src` specifically to build this runtime, separately from any grammar.

---

## 1. Obtain the Tree‑sitter Core Runtime Source

Clone the core repo (do **not** confuse this with this project's own `tree-sitter/` folder, which is the CNL grammar, not the runtime):

```bash
git clone --depth 1 https://github.com/tree-sitter/tree-sitter.git ts-core
```

The runtime source is `ts-core/lib/src/` (13 `.c` files, amalgamated by `lib.c` via `#include`) and the public header is `ts-core/lib/include/tree_sitter/api.h`.

`ts-core/lib/src/wasm_store.c` guards its WASM‑specific code behind `#ifdef TREE_SITTER_FEATURE_WASM` — leave that macro undefined; it compiles to a no‑op stub without needing `wasmtime`/`wasm.h`, and Limelight‑X's CNL grammar has no WASM injection needs.

---

## 2. Create the Export Definition File

`api.h` has **no `__declspec(dllexport)`/`_WIN32` handling at all** (confirmed by reading it) — MSVC does not export symbols from a DLL by default the way GCC/Clang do, so without an explicit export list the resulting DLL would have the same "nothing exported" problem `tree-sitter-limelightx.dll` has. Create `ts-runtime.def` listing every function Limelight‑X's `NativeMethods.cs` P/Invokes:

```
LIBRARY tree-sitter-runtime
EXPORTS
    ts_parser_new
    ts_parser_delete
    ts_parser_set_language
    ts_parser_parse_string
    ts_tree_root_node
    ts_tree_delete
    ts_node_type
    ts_node_start_byte
    ts_node_end_byte
    ts_node_start_point
    ts_node_end_point
    ts_node_child
    ts_node_child_count
    ts_node_parent
    ts_node_descendant_for_byte_range
    ts_node_is_error
    ts_node_is_missing
    ts_node_is_null
    ts_query_new
    ts_query_delete
    ts_query_cursor_new
    ts_query_cursor_delete
    ts_query_cursor_exec
    ts_query_cursor_next_match
    ts_query_cursor_next_capture
    ts_query_capture_name_for_id
```

Add a symbol here whenever `NativeMethods.cs` gains a new `[DllImport("tree-sitter-runtime.dll")]` — an un-exported symbol fails at P/Invoke call time with `EntryPointNotFoundException`, not at compile time.

---

## 3. Build (ARM64, current target — see `tree-sitter-build-guide.md` §0 for the ARM64-now/x64-later staging, which applies identically here)

Open **ARM64 Native Tools Command Prompt for VS 2022** (or run via `vcvarsarm64.bat`), then from the directory containing `ts-runtime.def`:

```bash
cl /LD /O2 /std:c17 /I "ts-core\lib\include" /I "ts-core\lib\src" "ts-core\lib\src\lib.c" /Fe:tree-sitter-runtime.dll /link /DEF:ts-runtime.def
```

Notes:
- `/std:c17` is required — `tree_cursor.c` uses `_Static_assert`, which MSVC's default (pre‑C11) mode rejects with `error C2143`/`C2059`.
- Compiling `lib.c` alone (the amalgamation) pulls in all 13 runtime `.c` files via its own `#include`s — do not compile the individual files separately as well, or you'll get duplicate‑symbol link errors.
- Verify the result with `dumpbin /exports tree-sitter-runtime.dll` — expect exactly the symbols listed in the `.def` file (26 as of this writing; update as `NativeMethods.cs` gains more P/Invokes).

---

## 4. Copy to the Avalonia Project

```
ui/native/tree-sitter-runtime.dll
```

Alongside `ui/native/tree-sitter-limelightx.dll`. `LimelightX.UI.csproj` copies both (see its `native\*.dll` `<None Include>` items).

---

## 5. How the Two DLLs Work Together

```csharp
[DllImport("tree-sitter-runtime.dll")]
public static extern IntPtr ts_parser_new();

[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr tree_sitter_limelightx();

[DllImport("tree-sitter-runtime.dll")]
public static extern bool ts_parser_set_language(IntPtr parser, IntPtr language);
```

```csharp
var parser = ts_parser_new();                      // from the runtime DLL
var language = tree_sitter_limelightx();            // from the grammar DLL
ts_parser_set_language(parser, language);            // runtime consumes the grammar's TSLanguage*
```

The grammar DLL is data (a `TSLanguage*` table) fed into the runtime DLL's engine — never the reverse, and never merged into one binary. This is the standard Tree‑sitter embedding pattern (the same one every language binding — Rust, Node, Python, WASM — follows): one runtime, N grammar plugins.

---

## 6. Verified End‑to‑End (Not Just Exports)

Exporting the right symbols isn't sufficient proof the two DLLs actually interoperate (ABI/calling‑convention mismatches wouldn't show up in `dumpbin`). This was verified with an actual parse: a throwaway console app loading both DLLs, calling `ts_parser_set_language` (returned `true`), `ts_parser_parse_string`, and walking the resulting tree with `ts_node_child`/`ts_node_type`/`ts_node_start_byte`/`ts_node_end_byte` produced a real, structurally sensible CST.

That same test surfaced a **separate, pre‑existing grammar defect — broader than it first looked**: `Load the article from "article.txt".` currently produces an `ERROR` node, because `grammar.js`'s `resource`/`target`/`format_target`/`language` rules have no keyword‑boundary guard (unlike the PEG spec's `!KeywordWord`) — the free‑text regex greedily consumes through the following keyword.

Follow‑up testing (`ui/tests/Intellisense/QueryRunnerTests.RunInjections_PromptHole_CurrentlyProducesNoPromptHoleNodeAtAll`) showed this isn't limited to keyword collisions: `resource`'s unbounded‑greedy regex also out‑competes `pronoun` and `name` for the grammar's `input` choice, because Tree‑sitter's lexer conflict resolution prefers the **longest** match, and `resource`'s `[^.\n]+` is always at least as long as any fixed‑literal alternative sharing that starting position. `Summarize it using {{ prompt: "..." }}.` never produces a `pronoun` node for "it" or a `prompt_hole` node at all — `resource` swallows "it using {{ prompt: \"" whole. There is no valid multi‑token CNL sentence shape that avoids this: anywhere `resource` is a legal alternative, it wins, so essentially any sentence with a trailing clause after free text (`from`, `using`, `to`, `as`, or a second choice alternative like `pronoun`/`name`) is affected — not a narrow edge case.

This is a grammar bug, not a runtime/build bug, and is tracked separately (see `spec/cnl-editor-architecture.md` §5's "Known Current Divergence" entry for the canonical pointer to this issue — this document remains the fullest write‑up of the empirical findings themselves, but is scoped to the runtime DLL build otherwise). Fixing it properly likely needs either an external scanner (`scanner.c`) implementing the PEG spec's negative‑lookahead semantics directly, or restructuring the free‑text rules to something Tree‑sitter's longest‑match lexer won't miscompare against fixed literals — both are real grammar‑engineering work, not a one‑line tweak.

A third finding (`ui/tests/Intellisense/FoldingServiceTests.GetFolds_TwoSentenceDocument_ReturnsExactlyTwoFoldRegions`) shows the blast radius is worse than "one malformed sentence gets an internal `ERROR` node": parsing `Load the article from "a.txt".\nSummarize it.` as a two‑sentence document drops the *entire first sentence* out of the CST — `program`'s `repeat($.sentence)` yields only one top‑level `sentence` node (`"Summarize it."`), not two. Tree‑sitter's error recovery apparently can't resynchronize back onto a clean `sentence` boundary once `resource` has swallowed through the terminating `"."` character position's surrounding structure, so it absorbs the malformed sentence into error‑recovery state that only resolves at the *next* recognizable sentence start, silently losing the first one rather than isolating it as a same‑level `ERROR` sibling. `[^.\n]+` still can't cross the literal `.`/`\n` characters themselves, so this is bounded to at most one sentence's worth of loss per occurrence — but confirms this bug is not safe to reason about as "contained within the one bad sentence."

A fourth finding, discovered while investigating `ui/tests/Intellisense/CompletionServiceTests.GetCompletions_AfterLoadStatementResource_SuggestsOnlyFromKeyword` (bdd-ui-interactions.md §2.12), escalates this further and is the most severe yet: even the **fully well‑formed** canonical example `Load the article from "a.txt".` — complete, correctly terminated, the exact sentence used throughout every spec doc — does not produce a clean `load_stmt` node at all. Empirically (a throwaway diagnostic dumping the full node tree), it parses as a single top‑level `ERROR[0-30]` wrapping only scattered literal‑token fragments (`Load the`, an unrelated `.` at the string's internal period, another nested `ERROR`, the closing `"`, the final `.`) with no `resource`, `string`, or `load_stmt` node anywhere in the tree. A "trial‑insertion" completion strategy (reparsing with each candidate keyword spliced in at the cursor, per §2.12's "AS MEASURED BY... none whose insertion would produce a new Tree‑sitter `ERROR` node" wording) was also tested and found to give **no usable signal**: inserting the grammatically‑correct `from` and an arbitrary non‑grammar word `xyz` at the same cursor position produce structurally indistinguishable trees (both just extend the same opaque top‑level `ERROR` span). Concretely, this means `load_stmt` — and very likely every other statement type, since they all route through the same `resource`/`input` rules — currently has **no CST‑derivable signal for grammar‑aware completions at all**, not merely a degraded one. Unlike the first three findings, no test input exists that both (a) matches this BDD scenario's shape (cursor after a free‑text position, expecting a specific next keyword) and (b) avoids the bug, since that shape *is* the bug. `CompletionService`'s real implementation is blocked on the grammar fix for any statement type exercising `resource`/`target`/`format_target`/`language`, not just deferred as a nice-to-have.

### Fix Applied, Rebuilt, and Verified

`tree-sitter/grammar.js` (and its `spec/parsing/grammer-js.md` mirror) were patched: `resource`/`target`/`format_target`/`language` now tokenize word‑by‑word via a shared hidden rule instead of one greedy whole‑run regex:

```js
resource: $ => prec.right(repeat1($._free_text_word)),
// ...same for target/format_target/language

_free_text_word: $ => token(prec(-1, /[^\s.\n]+/)),
```

Every fixed string literal in this grammar (`"from"`, `"using"`, `"Load the"`, etc.) is precedence 0 by default; `_free_text_word` is explicitly precedence ‑1. Tree‑sitter's lexer conflict resolution checks precedence *before* match length, so at any position where a keyword literal could start, it now wins outright over continuing as another free‑text word — this is the standard "reserved word" idiom for exactly this kind of ambiguity. `pronoun`/`name` were *initially* left unchanged on the same reasoning; `pronoun` turned out to be fine, `name` did not - see the fifth finding and second fix below.

The fix was written without being able to compile/test it directly, then handed off, rebuilt (`tree-sitter generate` + `cl /LD` per `tree-sitter-build-guide.md`), and re‑verified empirically against all four findings above, in order:

1. **Confirmed.** `Load the article from "article.txt".` now parses as `program[0-36] > sentence[0-36] > load_stmt[0-36]` with `resource[9-16]` ("article"), `from[17-21]`, `string[22-35]` (`"article.txt"`, period safely inside the quotes), `.[35-36]` — zero `ERROR`/`MISSING` nodes anywhere.
2. **Confirmed.** `Summarize it using {{ prompt: "Summarize in 3 bullets." }}.` now parses "it" as a real `pronoun[10-12]` node (not swallowed by `resource`) and produces a real `prompt_hole[19-58]` with its inner `string[30-55]` correctly captured by both `highlights.scm`'s `@embedded`/`@string` and `injections.scm`'s `@injection.content`.
3. **Confirmed.** `Load the article from "a.txt".\nSummarize it.` now yields exactly `sentence[0-30]` and `sentence[31-44]` — 2 top‑level sentences, zero errors in either.
4. **Confirmed, with a caveat worth recording.** The raw incomplete text `Load the article ` (nothing appended) still produces an opaque `ERROR` with no `resource` node — Tree‑sitter's error‑recovery heuristics apparently still prefer a minimal-error-span parse when the input just ends mid‑construct. But the trial‑insertion signal itself now works: reparsing `Load the article from` (candidate `from` appended) produces `ERROR[0-21] > Load the[0-8], resource[9-16], from[17-21]` — a real `resource` node AND a real `from` token, both now visible as recognized children even though the statement is still incomplete (missing string/period) — while `Load the article xyz` (an invalid candidate) still produces only `ERROR[0-20] > Load the[0-8]`, nothing else. The two are now structurally distinguishable, which is exactly the signal `CompletionService`'s trial‑insertion strategy needs; it just has to reparse-with-each-candidate rather than reading the unmodified current parse directly.

`ui/tests/Intellisense/ParserHostTests.cs`'s `Parse_LoadStatementWithQuotedPathContainingPeriod_ProducesCleanLoadStmt` (renamed/inverted from `..._CurrentlyProducesErrorNode`) and `QueryRunnerTests.cs`'s `RunInjections_PromptHole_InjectsOnlyTheQuotedStringContent` (renamed/inverted from `..._CurrentlyProducesNoPromptHoleNodeAtAll`) now assert this fixed behavior and pass.

### Fifth Finding: `name`'s Default Precedence Also Truncated Multi‑Word Resources

Discovered while building variable hover (`ui-intellisense-engine-spec.md` §7.1), whose own spec example is `Let article be the text from "article.txt".` — exercising `bind_stmt`'s `choice($.resource_from, $.expression)` for the first time in this whole effort. Empirically:

```
Let article be the text from "article.txt".
→ bind_stmt[Let, name="article", be, expression→name="the", ERROR(everything after "the")]
```

`"the"` (the first word of what should be a 2‑word `resource`, `"the text"`) got consumed whole as `expression`'s `name` alternative, because `name`'s *default* precedence (0, same as keyword literals) beat `_free_text_word`'s ‑1 - the identical mechanism as the original bug, just at a different ambiguous position (`resource_from` vs `expression`, not free‑text vs. keyword). The same defect independently broke `input`'s `choice($.resource, $.name, $.pronoun)` for **any** multi‑word resource whose first word is identifier‑shaped - including `cnl-grammar.md` §2.2's own canonical example:

```
Extract the entities from the article.
→ extract_stmt[Extract the, target="entities", from, input→name="the", ERROR(" article.")]
```

Neither of these was caught by any of the first four findings' tests, none of which exercised `resource_from` or a multi‑word `input` resource - a real blind spot in the original verification pass.

### Second Fix Applied, Rebuilt, and Verified

`name` changed from a bare regex (implicit precedence 0) to explicit precedence ‑2 - one level below `_free_text_word`'s ‑1:

```js
name: $ => token(prec(-2, /[A-Za-z_][A-Za-z0-9_]*/)),
```

At the two ambiguous positions, the lexer now prefers continuing as a free‑text word over reducing to a standalone name, so multi‑word resources are never truncated regardless of whether their first word happens to be identifier‑shaped. `pronoun` needed no corresponding change (its literals are precedence 0 by default, unaffected, and already confirmed correct by the first fix's verification pass). `name` in `bind_stmt`'s unambiguous first slot (`"Let" $.name "be"`) is unaffected - no competing alternative exists there regardless of precedence.

Re‑verified after rebuild:

1. **Confirmed.** `Let article be the text from "article.txt".` now parses as a clean `bind_stmt` with `resource_from → resource[15-23]` ("the text") + `from` + `string`, zero `ERROR` nodes.
2. **Confirmed.** `Extract the entities from the article.` now parses as a clean `extract_stmt` with `input → resource` spanning "the article" (2 words), zero `ERROR` nodes.
3. **Confirmed accepted narrowing.** `Let summary be article.` (bare name reference, no `from`, not a pronoun) now fails to parse - the lexer commits to a free‑text‑word token for "article" before the parser can know `resource_from` will dead‑end reaching for a `from` that never comes, and `expression`'s `name` alternative can't consume an already‑lexed `_free_text_word` token. No spec example anywhere uses this exact shape. Accepted as the right tradeoff against the much more common and more severe multi‑word‑truncation bug it replaces - see `spec/cnl-editor-architecture.md` §5's "Follow‑Up" entry for the full reasoning. Variable hover is designed around this: it matches node *text* against known bindings for both `resource`‑ and `name`‑typed nodes, not on the node specifically being `name`‑typed, so it doesn't depend on which way this tie resolves.

---

## 7. Summary

1. Clone `tree-sitter/tree-sitter` (core) — separate from this repo's own `tree-sitter/` (the CNL grammar).
2. Write `ts-runtime.def` listing every P/Invoked `ts_*` symbol.
3. `cl /LD /O2 /std:c17 ... lib.c /Fe:tree-sitter-runtime.dll /link /DEF:ts-runtime.def`
4. Copy to `ui/native/tree-sitter-runtime.dll`.
5. Load both DLLs; the grammar DLL's `TSLanguage*` feeds the runtime DLL's `ts_parser_set_language`.

This is a one‑time build (the runtime doesn't change when the CNL grammar changes) — re‑run only if the set of P/Invoked `ts_*` functions grows, or if a `win-x64` build is added later (see `tree-sitter-build-guide.md` §9's staging note, which applies to this DLL too).
