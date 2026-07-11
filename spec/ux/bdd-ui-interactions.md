# BDD — UI Interactions (Streaming Edition)

## Purpose
This document defines all BDD interaction scenarios for the Limelight‑X UI.  
It specifies deterministic user interactions, execution workflows, streaming behavior, inspector updates, navigation constraints, and error handling under the **event‑streaming API**.

This specification is authoritative.  
All implementation must follow these scenarios exactly.

---

# 1. Conventions

Each scenario uses the extended BDD format:

- **GIVEN** (initial state)  
- **WHEN** (user action or backend event)  
- **THEN** (UI reaction)  
- **SO THAT** (user‑visible outcome)  
- **AS MEASURED BY** (deterministic observable behavior)

All scenarios assume:

- app‑wide single‑execution mode (one execution in flight at a time, across all tabs)  
- deterministic MVVM state  
- incremental WebSocket event streaming  
- correlation‑ID filtering  
- no parallel executions  
- tab switching, opening, and closing are never blocked by execution state  

---

# 2. Editor Interactions

## 2.1 Editing CNL Text
**GIVEN** the user has a `.llx` tab active  
**WHEN** they type valid CNL text  
**THEN** the editor updates `SourceText`  
**SO THAT** the UI reflects the new content  
**AS MEASURED BY** syntax highlighting and updated validation state

## 2.2 The Editor Never Calls the Backend on Its Own
**Status: FINAL.** Supersedes an earlier "Live Validation" design that called `/explain` on a debounce timer after every keystroke, populating `EditorViewModel.ValidationErrors`/`ErrorBanner` above the editor — removed entirely (`EditorViewModel` no longer takes `IPipelineService`/`IEventStreamService` at all).  
**GIVEN** the user modifies CNL text in a `.llx` tab  
**WHEN** no explicit Run or Explain click has occurred  
**THEN** no HTTP request or WebSocket call is made — `RefreshDecorations()` (§2.7a) still runs synchronously and updates `LocalDiagnostics`/`QuickFixes`/`FoldRegions`/`Outline`/`GhostSuggestion` entirely from the local Tree‑sitter CST  
**SO THAT** the backend is reached only when the user actually asks for it, matching `cnl-editor-architecture.md` §5's two‑independent‑parsers model: Tree‑sitter's local squiggle+hover (§2.16–§2.17) is the real‑time syntax-error surface, and the backend is authoritative only for what the user explicitly ran or explained  
**AS MEASURED BY** zero calls to `IPipelineService`/`IEventStreamService` as a result of `EditorViewModel.Text` changing, for any number of keystrokes; the only two code paths that ever reach the backend are `RunRequested`/`ExplainRequested`, invoked exclusively by `RunCommand`/`ExplainCommand` (§2.3), which `CnlTabViewModel` wires to `PipelineExecutionViewModel.RunPipelineAsync`/`ExplainPipelineAsync` — the one and only backend-calling path for a `.llx` tab. Backend errors from an explicit Run/Explain click surface via `PipelineExecutionViewModel.ErrorBanner` (§6.1, unchanged) — there is no separate editor-level banner for backend errors.

## 2.3 Run/Explain Disabled During Execution
**GIVEN** the user clicks Run  
**WHEN** execution begins  
**THEN** Run and Explain disable — in this tab **and every other open tab**  
**SO THAT** no parallel executions occur  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == true`

## 2.4 Disable Execution Buttons App‑Wide When Any Execution Starts
**GIVEN** the user has a `.llx` tab active  
**AND** Run and Explain are enabled in every open tab  
**WHEN** the user clicks **Run** or **Explain** in any tab  
**THEN** Run and Explain become disabled immediately in **every** open tab  
**SO THAT** no parallel or overlapping executions can occur  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == true` and `CanExecute == false` for every tab's Run and Explain commands

---

## 2.5–2.29 Tree‑sitter Editor Decoration

> **Status: §2.5–§2.19 FINAL (implemented).** Authored to close the gap identified when reviewing `spec/cnl-editor-architecture.md`: no BDD source existed anywhere for syntax highlighting migration, folding, hover, or completion before this addition. Cross-checked against `ui-viewmodels.md` §6 and `ui-components.md` §4.2 (which were extended with matching `CompletionItems`/`HoverInfo`/`QuickFixes` state) and, in this pass, against `FoldRegions`/`LocalDiagnostics` (§2.7–§2.9) — no drift found; `HoverInfo` is confirmed nullable (`null` = no hover), `CompletionItems` is confirmed to be the same `ObservableCollection<CompletionItem>` type, and `FoldRegions`/`LocalDiagnostics` are confirmed to be `ObservableCollection<FoldRegion>`/`ObservableCollection<LocalDiagnostic>` on `EditorViewModel`. These 15 scenarios are the executable spec for the tests in `ui/tests/Edit/EditorViewModelTests.cs` and `ui/tests/Intellisense/` — one test per scenario, per `CLAUDE.md` §6.
>
> **Amendment (§2.16–§2.19 added):** closes the "VS Code-style editor" gap — squiggly rendering, hover-shows-diagnostic-message, ghost-text suggested fixes, and Tab-to-accept. §2.7's "not a validation‑error style" wording is clarified below to mean the *data-model* separation (`LocalDiagnostics` must never flow into any backend-authoritative error state) — it was never a constraint on visual shape, and `ui-components.md` §4.2 already anticipated an "advisory squiggle." `RefreshDecorations()` is now triggered directly by `OnTextChanged` (§2.7a) rather than being explicit-call-only, since local diagnostics must be visible as the user types, matching the editors this feature is modeled on.
>
> **Amendment (§2.20–§2.29 added, PROPOSED — NOT YET IMPLEMENTED):** backlog scenarios from an IDE-capability gap review, guiding the next phase of IntelliSense work. Unlike §2.5–§2.19, these describe code that does not exist yet (`CompletionService.cs`, `HoverService.cs`, `CompletionItem.cs`, and `ParserHost.cs` are all untouched as of this amendment). Each carries its own **Status**/**Note** line recording exactly what's missing. §2.20–§2.22 close a gap in `ui-intellisense-engine-spec.md` §5.1/§5.3 itself — variable and prompt-template completions were already specified there but never built. §2.23–§2.27 and §2.29 are net-new IDE conveniences (sentence-skeleton snippets, auto-closing pairs, Go to Definition, Find All References, auto-trigger completion) proposed by analogy to mainstream editors, kept inside the existing "CST-only, syntactic, advisory" boundary (§12 Non-Goals) — none of them call `/src/api` or require the AST Normalizer. §2.28 reuses the squiggle/margin-marker/hover-priority rendering §2.16–§2.17 already ship, adding only a new `DiagnosticService` yield condition. Two related capabilities — Rename Symbol and an undefined-variable-reference diagnostic — are deliberately **not** included here because they need a product decision first (Rename mutates document text, the first editor service to do so; the undefined-variable diagnostic brushes against §12's "no semantic diagnostics" boundary) rather than being a spec gap like §2.20–§2.22.
>
> Scope premise (per `cnl-editor-architecture.md` §1, §5, as clarified): Tree‑sitter is client‑side‑only editor decoration. It never calls, and is never called by, `/src/api` or Rust. It does not participate in backend validation or execution. Everything below is local computation, keyed off the CST that `spec/parsing/grammer-js.md` produces from `SourceText`.

## 2.5 Tree‑sitter Highlighting Replaces the Hand‑Coded Tokenizer
**GIVEN** a `.llx` tab is open  
**WHEN** the user types text matching a CNL keyword, pronoun, resource, string, or expression‑hole token  
**THEN** the token is colored using Tree‑sitter's highlight query (`spec/parsing/highlights-scm.md`) against the CST, in place of `SyntaxHighlighter.Tokenize`  
**SO THAT** highlighting becomes grammar‑derived instead of hand‑coded, with no user‑visible change in token classes or colors  
**AS MEASURED BY** each span's color still matching the existing `TokenKind` → brush mapping (`SyntaxColors.axaml`), with span boundaries now taken from Tree‑sitter node boundaries instead of `SyntaxHighlighter`'s

## 2.6 Expression Hole Content Renders via Injection
**GIVEN** a sentence contains `{{ prompt: "..." }}`  
**WHEN** the editor renders it  
**THEN** the quoted string inside the hole is decorated via `spec/parsing/injections-scm.md`'s plain‑text injection, distinct from the surrounding `{{`, `prompt:`, `}}` glyphs  
**SO THAT** the literal prompt text reads as plain content rather than further CNL grammar — matching `SyntaxHighlighter.TokenizeExpressionHole`'s existing split byte‑for‑byte  
**AS MEASURED BY** the injected `(string)` node receiving `String` styling while the hole's structural glyphs receive `ExpressionHole` styling

## 2.7 Highlighting Degrades Gracefully on Invalid or Incomplete Text
**GIVEN** the user is mid‑edit and the current sentence is incomplete (e.g. `Load the article from`)  
**WHEN** Tree‑sitter reparses  
**THEN** Tree‑sitter produces an `ERROR`/`MISSING` node scoped to the incomplete region only, and every token it can still classify outside that region keeps its normal highlight color  
**SO THAT** one incomplete sentence never blanks out highlighting for the rest of the document  
**AS MEASURED BY** tokens before/after the error region retaining their `TokenKind` color; the error region itself represented as one entry in `EditorViewModel.LocalDiagnostics` (§2.8) — rendered with its own advisory squiggle styling (§2.16), which visually resembles `PipelineExecutionViewModel.ErrorBanner`'s authoritative styling (`ui-error-handling.md` §10.3) by deliberate shared design, without either data source ever writing into the other (§2.8)

## 2.7a `RefreshDecorations` Runs on Every Text Change
**GIVEN** a `.llx` tab is open  
**WHEN** the user's edit changes `EditorViewModel.Text`  
**THEN** `RefreshDecorations()` runs synchronously within `OnTextChanged`, recomputing `LocalDiagnostics`, `QuickFixes`, `FoldRegions`, and `Outline` from the current parse  
**SO THAT** squiggles, hover messages (§2.17), and ghost‑text suggestions (§2.18) are never stale relative to what's on screen, the same way a real language editor's diagnostics track keystrokes  
**AS MEASURED BY** `LocalDiagnostics`/`QuickFixes`/`FoldRegions`/`Outline` reflecting the latest `Text` immediately after `Text` is set, with no separate explicit `RefreshDecorations()` call required by the caller — this supersedes this class's earlier "explicit‑call‑only" scope note, which existed only because no production call site existed yet

## 2.8 Local Diagnostics Are the Editor's Only Error Surface Until Run/Explain Is Clicked
**Status: FINAL.** Supersedes an earlier scenario ("Local Error Squiggles Never Replace `/explain` Validation") written for the removed Live Validation design, where `EditorViewModel` kept its own `SyntaxErrors`/`ValidationErrors` collection populated by a background `/explain` call running concurrently with Tree‑sitter. That collection no longer exists (§2.2) — there is nothing left to reconcile against.  
**GIVEN** the user types invalid CNL text  
**WHEN** Tree‑sitter's next in‑process reparse produces an `ERROR`/`MISSING` node  
**THEN** `EditorViewModel.LocalDiagnostics` gains an entry for that node's span immediately, and this remains true for as long as the user keeps editing without clicking Run or Explain  
**SO THAT** the user gets fast, purely local visual feedback with no backend round‑trip in the loop at all  
**AS MEASURED BY** `LocalDiagnostics` reflecting only the current parse's `ERROR`/`MISSING` nodes, updating on every keystroke via `RefreshDecorations()` (§2.7a); no other error collection exists on `EditorViewModel` for it to disagree with. Once the user does click Run or Explain, `PipelineExecutionViewModel.ErrorBanner` (§6.1) is the sole authoritative error surface for that execution — it and `LocalDiagnostics` remain independent (Tree‑sitter's grammar and the Rust parser's grammar can still disagree, `cnl-editor-architecture.md` §5), but nothing in the UI attempts to reconcile them.

## 2.9 Folding: One Region Per Sentence
**GIVEN** a `.llx` tab contains two or more CNL sentences  
**WHEN** the editor renders  
**THEN** a fold control appears at the start of each sentence, per `spec/parsing/folds-scm.md`'s `(sentence) @fold` query, backed by one entry per sentence in `EditorViewModel.FoldRegions`  
**SO THAT** the user can collapse individual sentences in a long CNL program  
**AS MEASURED BY** `EditorViewModel.FoldRegions.Count` equal to the number of top‑level `sentence` CST nodes, each entry's `[StartByte, EndByte)` matching one sentence's span, collapsing to a single summary line when toggled closed, with sentences outside it unaffected

## 2.10 Structural Selection Expands to the Enclosing Grammar Node
**GIVEN** the cursor is positioned inside a token within a sentence (e.g. inside a quoted string)  
**WHEN** the user invokes "expand selection" repeatedly  
**THEN** the selection grows to the smallest enclosing CST node, then that node's parent, and so on, up to the enclosing `sentence`  
**SO THAT** the user can select structurally meaningful units without manual click‑dragging  
**AS MEASURED BY** `SelectionRange` matching the `[start, end)` byte offsets of the enclosing CST node at each expansion step, in strict child‑to‑parent order, never skipping or repeating a node

## 2.11 Hover Shows Grammar Node Info
**GIVEN** the user hovers the pointer over a non‑whitespace token  
**WHEN** the token resolves to a CST node  
**THEN** a tooltip appears showing that node's grammar role (e.g. "keyword", "pronoun", "resource", "expression hole")  
**SO THAT** the user can learn CNL's grammar without leaving the editor  
**AS MEASURED BY** `EditorViewModel.HoverInfo.Text`/`Position` reflecting the hovered node's kind and span; `HoverInfo == null` when hovering whitespace between tokens

## 2.12 Completions Suggest Valid Next Tokens by Position
**GIVEN** the cursor is at a position where the grammar allows only a closed set of next tokens (e.g. immediately after `Load the article `, where `LoadStmt` allows only `from` next)  
**WHEN** the user triggers completion  
**THEN** `EditorViewModel.CompletionItems` is populated with exactly the grammar‑valid keyword(s)/pronoun(s) for that position  
**SO THAT** the user is guided through valid CNL sentence shapes without memorizing them  
**AS MEASURED BY** `CompletionItems` containing only tokens that `peg-grammar.md`'s rules allow at that cursor position, and none whose insertion would produce a new Tree‑sitter `ERROR` node

## 2.13 Completions Are Empty Inside Free‑Text Positions
**GIVEN** the cursor is positioned inside a `resource`/`target`/`format_target`/`language` span (free‑text noun phrase)  
**WHEN** the user triggers completion  
**THEN** `CompletionItems` remains empty  
**SO THAT** the editor never fabricates suggestions for content the grammar deliberately leaves unconstrained (`cnl-editor-architecture.md` §1.1.3) — this also holds for anything requiring the AST Normalizer (e.g. suggesting a specific bound variable name or a pronoun's resolved target), since Tree‑sitter has no access to normalization  
**AS MEASURED BY** `CompletionItems.Count == 0` while the cursor resolves inside one of those four free‑text node kinds

## 2.14 Editor Decoration Is Never Blocked by the Execution Lock
**GIVEN** a pipeline execution is running in some tab (`IExecutionLockService.IsAnyExecutionRunning == true`)  
**WHEN** the user types in any tab's editor, triggering highlighting, folding, hover, or completion  
**THEN** all four continue to update normally  
**SO THAT** purely local editor decoration is never gated by a lock that exists solely to serialize backend calls  
**AS MEASURED BY** highlight spans, `FoldRegions`, `LocalDiagnostics`, `HoverInfo`, and `CompletionItems` all updating on keystroke regardless of `IExecutionLockService.IsAnyExecutionRunning`'s value

## 2.15 Deterministic Reparse
**GIVEN** identical CNL source text  
**WHEN** Tree‑sitter parses it twice (e.g. on initial load, and again after an undo that restores the exact same text)  
**THEN** the resulting CST, highlight spans, fold regions, and completion/hover results are identical both times  
**SO THAT** editor decoration never flickers or varies for unchanged input, consistent with `CLAUDE.md` §3.3 and the determinism guarantee the hand‑coded `SyntaxHighlighter` it replaces already provided  
**AS MEASURED BY** byte‑for‑byte identical token spans/kinds and fold region boundaries across both parses of the same text

## 2.16 Local Diagnostics Render as a Squiggly Underline with a Margin Marker
**GIVEN** `EditorViewModel.LocalDiagnostics` contains an entry for an `ERROR` or `MISSING` node's span  
**WHEN** the editor renders  
**THEN** `LocalDiagnosticsRenderer` draws a red squiggly (zig‑zag) underline beneath the span, using `SyntaxErrorBrush`, plus a red margin marker glyph on that line — matching `ui-error-handling.md` §10.3's authoritative styling in shape, while remaining backed by `LocalDiagnostics` only — `EditorViewModel` has no backend-sourced error state to confuse it with (§2.8)  
**SO THAT** Tree‑sitter‑derived errors read the same way any modern code editor renders a syntax error, instead of the prior translucent background wash  
**AS MEASURED BY** the renderer producing zig‑zag stroke geometry (not a filled rectangle) for each `LocalDiagnostics` entry's span, plus one margin marker per line containing at least one entry; a zero‑width `MISSING` span (`StartByte == EndByte`) still produces a visible minimum‑width squiggle and marker

## 2.17 Hovering a Local Diagnostic Shows Its Message, Taking Priority Over Grammar Hover
**GIVEN** the pointer hovers a byte covered by a `LocalDiagnostics` entry's `[StartByte, EndByte]` (inclusive of both ends, so a zero‑width `MISSING` span is still hoverable)  
**WHEN** hover resolves  
**THEN** `EditorViewModel.HoverInfo.Text` equals that diagnostic's `Message` and `HoverInfo.Position` equals its `StartByte`, taking priority over any grammar‑role hover (§2.11) that would otherwise apply at the same position  
**SO THAT** hovering an error explains what's wrong, through the same tooltip mechanism §2.11 already uses for grammar info  
**AS MEASURED BY** `HoverInfo` matching the diagnostic whenever the hovered byte falls inside its span, and falling back to `HoverService.GetHover`'s existing grammar‑role result everywhere else, including `null` when neither applies

## 2.18 A Self‑Describing Diagnostic Shows Ghost Text at the Fix Location
**GIVEN** a `MISSING` node's expected token is one of a fixed set of self‑describing grammar literals — missing period (`.`), missing closing quote (`"`), or missing closing `}}` for an expression hole — and `EditorViewModel.CursorPosition` sits exactly at that node's `InsertionByte`  
**WHEN** `RefreshDecorations()` (§2.7a) or a cursor move re‑evaluates the current position  
**THEN** `EditorViewModel.GhostSuggestion` holds a `QuickFixItem` whose `InsertText` is the missing literal, rendered inline in the editor as non‑editable, semi‑transparent text at the caret, with real document content still flowing normally around it  
**SO THAT** the user sees the fix the diagnostic's own message already implies, without leaving the editor — the same "ghost text" experience as VS Code / inline‑suggestion‑style editors  
**AS MEASURED BY** `GhostSuggestion.InsertText` equal to the expected literal when the caret is at a self‑describing `MISSING` node's position; `GhostSuggestion == null` for every other `MISSING`/`ERROR` case (an unrecognized missing token such as `from`/`as`/`Let`, a generic `ERROR` node, or the caret positioned elsewhere) — this list is exhaustive for v1, not a general‑purpose fix‑suggestion engine; `EditorViewModel.Text` is never mutated by ghost text appearing or disappearing

## 2.19 Tab Commits Ghost Text; Otherwise Falls Through Unhandled
**GIVEN** `EditorViewModel.GhostSuggestion` is non‑null  
**WHEN** the user presses `Tab` with no modifier keys held  
**THEN** `ApplyQuickFixCommand` runs with that `QuickFixItem`, splicing `InsertText` into `Text` at `InsertionByte`, moving the caret to just past the inserted text, and clearing `GhostSuggestion` to `null`  
**SO THAT** accepting a suggested fix matches the Tab‑to‑accept convention of VS Code / Copilot‑style inline suggestions  
**AS MEASURED BY** `Text` containing the inserted literal at the expected offset and `GhostSuggestion == null` immediately after commit; a companion case confirms that when `GhostSuggestion` is `null`, the key‑down handler leaves the event unhandled (`e.Handled == false`), so `Tab` falls through to the editor's existing indent‑insert behavior (`ui-accessibility.md` §2) rather than being silently swallowed

## 2.20 Completions Suggest Bound Variable Names Inside Resource Positions
**Status: PROPOSED — not yet implemented.** `CompletionService.cs`'s own doc comment currently excludes this ("'Variables'/'Prompt templates' are out of scope, nothing exercises them"), even though `ui-intellisense-engine-spec.md` §5.1/§5.2 already call for it ("Inside resource position → Suggest variables and pronouns").  
**GIVEN** the document contains `Let article be the text from "a.txt".` followed by a new sentence with the cursor inside an `input`/`resource` position  
**WHEN** the user triggers completion  
**THEN** `CompletionItems` includes `article` alongside the existing pronoun suggestions  
**SO THAT** the user can reference a variable they already bound without retyping or memorizing its exact spelling  
**AS MEASURED BY** `CompletionItems` containing one entry per distinct `bind_stmt` name bound anywhere before the cursor's byte offset, sourced the same way `HoverService.VariableBindingText` already resolves bindings — never a name bound *after* the cursor. This is CST‑only, best‑effort local scanning — the same class of computation §2.13 already blesses for `HoverService`, not a use of the AST Normalizer, so it does not conflict with §12's "no semantic completions" Non‑Goal.

## 2.21 Variable Completions Rank Above Pronoun Completions
**Status: PROPOSED — not yet implemented; also blocked on a data-shape gap.** `CompletionItem` (`ui/viewmodels/CompletionItem.cs`) currently has only `Text`/`Description` — no `Kind`/`Rank` field to sort by, so this scenario cannot be implemented until one is added.  
**GIVEN** the cursor is in a position where both a bound variable name and a pronoun are grammar‑valid  
**WHEN** the user triggers completion  
**THEN** `CompletionItems` lists the variable name(s) before any pronoun  
**SO THAT** the most specific, user‑authored reference is easiest to pick  
**AS MEASURED BY** `CompletionItems` index of any variable entry being lower than the index of any pronoun entry, per `ui-intellisense-engine-spec.md` §5.3 ("Variables rank above pronouns")

## 2.22 Completion Inside `using` Suggests a Prompt‑Hole Skeleton
**Status: PROPOSED — not yet implemented.** Also specified but unbuilt, per `ui-intellisense-engine-spec.md` §5.1/§5.2 ("Inside prompt hole → Suggest prompt templates").  
**GIVEN** the cursor is immediately after `using ` in a statement that allows `using_prompt`  
**WHEN** the user triggers completion  
**THEN** `CompletionItems` includes a `{{ prompt: "" }}` skeleton entry  
**SO THAT** the user doesn't have to remember the exact hole syntax  
**AS MEASURED BY** selecting that item inserting text that reparses with a valid `prompt_hole` node at the insertion point, cursor left between the empty string's quotes — structural skeleton only, no content suggestions, per §5.1

## 2.23 Selecting a Verb Completion Inserts a Sentence Skeleton
**Status: PROPOSED — not yet implemented; net-new, not previously specified.**  
**GIVEN** the cursor is at a sentence‑start position  
**WHEN** the user selects the `Load` completion item  
**THEN** the editor inserts `Load the  from "".` with the cursor placed inside the first blank (before `from`)  
**SO THAT** the user is guided through the full sentence shape, not just the next word  
**AS MEASURED BY** the inserted text reparsing with no `ERROR`/`MISSING` nodes once the blanks are filled, and the cursor's post‑insertion byte offset matching the start of the `resource` span — derived purely from `tree-sitter/grammar.js`'s seven statement rules, no semantics needed

## 2.24 Typing an Opening Quote Auto‑Inserts Its Match
**Status: PROPOSED — not yet implemented; net-new.** Also has a practical side benefit: it sidesteps `cnl-editor-known-issues.md` §1 in the common case, since a user who never leaves a string unterminated never hits the unreachable `MISSING '"'` diagnostic gap, independent of that GLR recovery issue ever getting root-caused.  
**GIVEN** the cursor is at any position where a `string` token is grammar‑valid  
**WHEN** the user types `"`  
**THEN** the editor inserts a matching `"` immediately after the cursor and leaves the cursor between the two quotes  
**SO THAT** the user doesn't have to type or align the closing quote manually  
**AS MEASURED BY** `Editor.Text` gaining exactly two `"` characters and `CaretOffset` sitting between them; typing a `"` immediately afterward moves past the auto‑inserted one instead of inserting a third

## 2.25 Typing `{{` Auto‑Inserts the Matching `}}`
**Status: PROPOSED — not yet implemented; net-new.** Complements, but is distinct from, the `}}` self‑describing‑fix/ghost‑text path (§2.18): that scenario covers *recovery* after the pair is already unbalanced, this one covers *prevention*.  
**GIVEN** the cursor is at a position where `using_prompt` is grammar‑valid  
**WHEN** the user types `{{`  
**THEN** the editor inserts `}}` immediately after the cursor  
**SO THAT** prompt holes are never left unclosed by a simple typo  
**AS MEASURED BY** `Editor.Text` containing a balanced `{{...}}` pair immediately after the keystroke, cursor positioned between them

## 2.26 Go to Definition Jumps From a Reference to Its Binding
**Status: PROPOSED — not yet implemented; net-new.**  
**GIVEN** the document contains `Let article be the text from "a.txt".` and a later sentence referencing `article`  
**WHEN** the user invokes Go to Definition on that later reference  
**THEN** the caret/selection moves to the `bind_stmt` that bound `article`  
**SO THAT** the user can find where a name came from without manually scrolling  
**AS MEASURED BY** `EditorViewModel.CursorPosition` (or `SelectionRange`) landing on the matching `bind_stmt`'s span, using the same nearest‑preceding‑binding resolution `HoverService.VariableBindingText` already implements (CST‑only, best‑effort — see §2.20's Non‑Goals note); no‑op (or a clear "no definition found" state) when no matching `bind_stmt` precedes the reference

## 2.27 Find All References Lists Every Use of a Bound Name
**Status: PROPOSED — not yet implemented; net-new.**  
**GIVEN** a document binds `article` once and references it in three later sentences  
**WHEN** the user invokes Find All References on any one of those four occurrences  
**THEN** all four spans (the binding plus all three references) are returned  
**SO THAT** the user can see every place a variable is used before renaming or removing it  
**AS MEASURED BY** the returned span count and byte ranges matching every `name`/`resource` node whose text equals the target, plus the originating `bind_stmt`

## 2.28 Diagnostic: Pronoun With No Preceding Sentence
**Status: PROPOSED — not yet implemented, but cheap: the rendering pipeline (§2.16 squiggle/marker, §2.17 hover priority) already exists and needs no changes; only `DiagnosticService` needs a new yield condition.**  
**GIVEN** a document's first sentence is `Summarize it.`  
**WHEN** Tree‑sitter reparses  
**THEN** `EditorViewModel.LocalDiagnostics` gains an advisory entry spanning the pronoun, with `SuggestedFix == null` (there is no literal to insert — this is a message‑only diagnostic, unlike the §2.18 self‑describing cases)  
**SO THAT** the user notices a pronoun with nothing to refer to before running the pipeline, without Tree‑sitter claiming to know what Rust's normalizer would actually do with it  
**AS MEASURED BY** one `LocalDiagnostics` entry appearing whenever the same nearest‑preceding‑sentence check `HoverService.PronounReferenceText` already performs returns "no preceding sentence," rendered through the same squiggle + margin‑marker + hover‑priority pipeline §2.16/§2.17 already ship — no backend-sourced error state exists to be affected either way (mirrors §2.8's separation)

## 2.29 Completion Triggers Automatically After a Verb‑Terminating Space
**Status: PROPOSED — not yet implemented.** `EditorViewModel.RequestCompletionsAt`'s doc comment still reads "explicit, not on every keystroke," unchanged even though `RefreshDecorations()` itself was just promoted from explicit‑only to automatic (§2.7a) for the diagnostics/folds/outline path — this scenario would extend that same promotion to completions.  
**GIVEN** the cursor is at a sentence‑start position  
**WHEN** the user types `Load the article ` (a space immediately after a token where the grammar allows exactly one next keyword)  
**THEN** the completion window opens automatically, without Ctrl+Space  
**SO THAT** the user is guided proactively rather than only on request  
**AS MEASURED BY** `CompletionItems` populating and the completion window opening within one debounce cycle of the triggering keystroke, without an explicit `RequestCompletionsAt` call from `CnlEditor`'s key‑down handler

---

# 3. Execution Interactions (Streaming)

## 3.1 Starting Execution
**GIVEN** the user clicks Run or Explain in a `.llx` tab  
**WHEN** the backend returns `{ accepted: true, correlation_id }`  
**THEN** the result renders in place inside that same tab's execution panel  
**SO THAT** the user sees pipeline progress without leaving the tab  
**AS MEASURED BY** that tab's `PipelineExecutionViewModel.IsRunning == true`; no tab or workspace-area change

## 3.2 Pipeline Started Event
**GIVEN** a `.llx` tab's execution panel is showing prior results  
**WHEN** `pipeline_started` arrives for that tab  
**THEN** that tab's inspector ViewModels clear  
**SO THAT** the tab begins a fresh execution  
**AS MEASURED BY** empty inspector panels in that tab only

## 3.3 Keep Buttons Disabled App‑Wide During Streaming
**GIVEN** execution has begun in some tab  
**WHEN** streaming events arrive (`pipeline_started`, `raw_ast_generated`, `normalized_ast_generated`, `ir_generated`, `prompt_generated`, `model_output_generated`)  
**THEN** Run and Explain remain disabled on every open tab  
**SO THAT** the user cannot trigger a new execution mid‑pipeline  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == true` throughout the entire event sequence

## 3.4 Progress Indicator Shows While This Tab Executes
**GIVEN** a `.llx` tab is idle  
**WHEN** the user clicks Run or Explain and `pipeline_started` arrives for that tab  
**THEN** that tab's progress indicator becomes visible  
**SO THAT** the user gets immediate feedback that their click registered  
**AS MEASURED BY** that tab's `PipelineExecutionViewModel.IsRunning == true` and its `LoadingIndicator.IsLoading == true`

## 3.5 Progress Indicator Hides When This Tab's Execution Ends
**GIVEN** that tab's progress indicator is visible  
**WHEN** its terminal event arrives (`final_result_ready`, `pipeline_failed`, or — for Explain — `normalized_ast_generated`)  
**THEN** the progress indicator hides in that tab at the same moment Run/Explain re‑enable app‑wide  
**SO THAT** the indicator never outlives the actual execution  
**AS MEASURED BY** `IsRunning == false` coinciding with `IExecutionLockService.IsAnyExecutionRunning == false`

---

# 4. Inspector Interactions (Incremental Updates)

All six panels are rendered from the moment the tab opens, starting `IsCollapsed == true` (`ui-components.md` §5.1). "Auto-expands" below means `IsCollapsed` transitions to `false`; the panel is never inserted or removed from the layout.

## 4.1 Raw AST Auto-Expands
**GIVEN** execution is running and RawAstPanel is collapsed  
**WHEN** `raw_ast_generated` arrives  
**THEN** RawAstPanel auto-expands  
**SO THAT** the user sees the first pipeline stage  
**AS MEASURED BY** `RawAstViewModel.IsCollapsed == false` and `RawAstViewModel.AstNodes.Count > 0`

## 4.2 Normalized AST Auto-Expands
**GIVEN** raw AST is expanded  
**WHEN** `normalized_ast_generated` arrives  
**THEN** NormalizedAstPanel auto-expands  
**SO THAT** the user sees normalized structure  
**AS MEASURED BY** `NormalizedAstViewModel.IsCollapsed == false` and `NormalizedAstViewModel.NormalizedNodes.Count > 0`

## 4.3 IR Auto-Expands
**GIVEN** normalized AST is expanded  
**WHEN** `ir_generated` arrives  
**THEN** IrPanel auto-expands  
**SO THAT** the user sees compiled IR  
**AS MEASURED BY** `IrViewModel.IsCollapsed == false` and `IrViewModel.Operations.Count > 0`

## 4.4 Prompts Auto-Expand
**GIVEN** IR is expanded  
**WHEN** the first `prompt_generated` arrives  
**THEN** PromptPanel auto-expands  
**SO THAT** the user sees model prompts  
**AS MEASURED BY** `PromptViewModel.IsCollapsed == false` and `PromptViewModel.Prompts.Count > 0`

## 4.5 Model Outputs Auto-Expand
**GIVEN** prompts are expanded  
**WHEN** the first `model_output_generated` arrives  
**THEN** ModelOutputPanel auto-expands  
**SO THAT** the user sees model responses  
**AS MEASURED BY** `ModelOutputViewModel.IsCollapsed == false` and `ModelOutputViewModel.Outputs.Count > 0`

## 4.6 Multiple Prompt/Output Pairs Accumulate Across a Multi-Step Trace
**GIVEN** execution is running for a program with two model-calling operations (e.g. `Summarize` → `Translate`)  
**WHEN** both `prompt_generated`/`model_output_generated` pairs arrive in order  
**THEN** `PromptViewModel.Prompts` and `ModelOutputViewModel.Outputs` each grow by one entry per event, without being cleared or reset between the two pairs, and neither panel re-collapses or re-expands after the first pair  
**SO THAT** the user sees every model call in a chained transformation, not just the last one  
**AS MEASURED BY** `PromptViewModel.Prompts.Count == 1` and `ModelOutputViewModel.Outputs.Count == 1` immediately after the first pair, and `PromptViewModel.Prompts.Count == 2` and `ModelOutputViewModel.Outputs.Count == 2` immediately after the second pair, with the first pair's entries still present and unchanged at both checkpoints, and `IsCollapsed == false` throughout for both panels

## 4.7 Final Result Auto-Expands
**GIVEN** execution is running  
**WHEN** `final_result_ready` arrives  
**THEN** FinalResultPanel auto-expands  
**SO THAT** the user sees the final pipeline output  
**AS MEASURED BY** `FinalResultViewModel.IsCollapsed == false` and `FinalResultViewModel.ResultText != null`

## 4.8 Re‑Enable Buttons App‑Wide Only After Completion or Error
**GIVEN** execution is running in some tab  
**WHEN** either  
- `final_result_ready` arrives, **or**  
- `pipeline_failed` arrives  
**THEN** Run and Explain re‑enable on every open tab  
**SO THAT** the user can start a new execution (in any tab) after the current one finishes  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == false` and `CanExecute == true` for Run and Explain on every tab

## 4.9 Awaiting-Model Indicator Shows After a Prompt Is Sent
**GIVEN** a trace is running  
**WHEN** `prompt_generated` arrives  
**THEN** the awaiting-model indicator (`ui-components.md` §4.4) becomes visible  
**SO THAT** the user gets feedback during the wait for a model call, since `ModelOutputPanel` itself stays collapsed until its first output arrives  
**AS MEASURED BY** `PipelineExecutionViewModel.IsAwaitingModelOutput == true`

## 4.10 Awaiting-Model Indicator Hides When Output Arrives
**GIVEN** the awaiting-model indicator is visible  
**WHEN** `model_output_generated` arrives for that operation  
**THEN** the indicator hides  
**SO THAT** the user isn't shown a stale "waiting" state once the output is already on screen  
**AS MEASURED BY** `PipelineExecutionViewModel.IsAwaitingModelOutput == false`

## 4.11 All Panels Reset to Collapsed on a New Run
**GIVEN** a tab's panels are expanded from a prior run (e.g. all six expanded after a completed trace)  
**WHEN** the user clicks Run or Explain again and `pipeline_started` arrives  
**THEN** all six panels collapse immediately, before any new event re-expands them  
**SO THAT** every run repeats the same closed→auto-expand reveal, regardless of how the previous run left the panels  
**AS MEASURED BY** `IsCollapsed == true` on all six inspector ViewModels at the moment `pipeline_started` is processed, before any subsequent event arrives

## 4.12 Prompt Panel Auto-Scrolls to the Newest Entry
**GIVEN** the Prompt panel is expanded and showing one or more prior prompts  
**WHEN** a new `prompt_generated` event appends another entry  
**THEN** the panel's content scrolls to reveal the newly appended entry  
**SO THAT** the user doesn't have to manually scroll to see the latest prompt in a multi-step trace  
**AS MEASURED BY** the panel's scroll position placing the newest `PromptViewModel.Prompts` entry within the visible viewport immediately after the append, unconditionally (no dependency on prior scroll position)

## 4.13 Model Output Panel Auto-Scrolls to the Newest Entry
**GIVEN** the Model Output panel is expanded and showing one or more prior outputs  
**WHEN** a new `model_output_generated` event appends another entry  
**THEN** the panel's content scrolls to reveal the newly appended entry  
**SO THAT** the user doesn't have to manually scroll to see the latest output in a multi-step trace  
**AS MEASURED BY** the panel's scroll position placing the newest `ModelOutputViewModel.Outputs` entry within the visible viewport immediately after the append, unconditionally (no dependency on prior scroll position)

---

# 5. Collapse/Expand Interactions

Inspector panels are always present in the layout (`ui-components.md` §5.1); collapse/expand toggles `IsCollapsed`, not the panel's presence.

## 5.1 Collapse Inspector
**GIVEN** an inspector panel is expanded  
**WHEN** the user clicks collapse  
**THEN** the panel collapses  
**SO THAT** the UI reduces vertical space  
**AS MEASURED BY** `IsCollapsed == true`

## 5.2 Expand Inspector
**GIVEN** an inspector is collapsed  
**WHEN** the user clicks expand  
**THEN** the panel expands  
**SO THAT** the user sees inspector contents  
**AS MEASURED BY** `IsCollapsed == false`

## 5.3 Collapse Does Not Block Updates
**GIVEN** an inspector is collapsed  
**WHEN** new streaming events arrive  
**THEN** the inspector ViewModel updates  
**SO THAT** collapse does not affect data flow  
**AS MEASURED BY** updated ViewModel state despite collapsed UI

---

# 6. Layout & Resize Interactions

## 6.1 Dragging the Editor/Panel Splitter Resizes Both Halves
**GIVEN** a `.llx` tab is open with the editor and execution panel at their current split  
**WHEN** the user drags the `SplitterControl` (`ui-components.md` §4.5) between the editor and the execution panel  
**THEN** the editor's and execution panel's heights update to reflect the new split  
**SO THAT** the user can allocate more or less space to editing versus inspecting results  
**AS MEASURED BY** `CnlTabViewModel.EditorPaneRatio` changing to match the drag, with the editor and execution panel heights recomputed from it

## 6.2 Editor Shows a Scrollbar Only When Content Overflows
**GIVEN** the editor's current split allocation is smaller than its `SourceText` content  
**WHEN** the tab renders (or the split ratio changes such that this becomes true)  
**THEN** the editor displays a vertical scrollbar on its right edge  
**SO THAT** the user can reach content that doesn't fit in the current allocation  
**AS MEASURED BY** the scrollbar's presence tracking exactly whether rendered content height exceeds the editor's current allocated height; no scrollbar when content fits

## 6.3 Dragging a Panel's Handle Trades Height With Its Immediate Neighbor
**GIVEN** the execution panel shows several expanded inspector panels  
**WHEN** the user drags one panel's `SplitterControl` handle (on its lower edge)  
**THEN** that panel's height changes and only its immediate next-neighbor panel below trades height with it (classic two-panel splitter behavior); panels further down are unaffected in size and simply reposition  
**SO THAT** the user can devote more space to one panel using a familiar, predictable resize interaction  
**AS MEASURED BY** the dragged panel's `Height` changing by the drag delta, its immediate neighbor's `Height` changing to compensate, panels beyond that neighbor keeping their own `Height` unchanged, and no change to any panel's `IsCollapsed` or content

## 6.4 Panel Resize Does Not Affect Collapse State or Content
**GIVEN** a panel is being resized via its handle  
**WHEN** the drag completes  
**THEN** the panel's `IsCollapsed` state and its displayed content are unchanged from before the drag  
**SO THAT** resizing is a purely visual/layout action, not a data or expand/collapse action  
**AS MEASURED BY** `IsCollapsed` and the panel's bound content collection/text being identical before and after the resize

---

# 7. Workspace Interactions

## 7.1 Tab Switching Is Never Blocked By Execution
**GIVEN** execution is running in some tab  
**WHEN** the user switches to a different tab, opens a new tab, or closes a different tab  
**THEN** the action succeeds immediately  
**SO THAT** the UI remains fully usable during a long‑running execution  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab`/`OpenTabs` change as requested, independent of `IExecutionLockService.IsAnyExecutionRunning`

## 7.2 Settings Blocked, Tabs Unaffected, During Execution
**GIVEN** execution is running in some tab  
**WHEN** the user clicks the Settings gear icon  
**THEN** the Settings modal does not open  
**AND** tab switching remains fully available  
**SO THAT** only backend-affecting actions are gated, not workspace navigation  
**AS MEASURED BY** `WorkspaceViewModel.IsSettingsOpen == false` while `ActiveTab` changes freely

## 7.3 Save and Save As Disabled With No Active Tab
**GIVEN** no tabs are open  
**WHEN** the user views the File menu  
**THEN** Save and Save As are disabled  
**SO THAT** the user cannot invoke a save with nothing to save  
**AS MEASURED BY** `SaveCommand.CanExecute == false` and `SaveAsCommand.CanExecute == false` while `WorkspaceViewModel.ActiveTab == null`

## 7.4 Save All Disabled With No Dirty Tabs
**GIVEN** one or more tabs are open, all with `IsDirty == false`  
**WHEN** the user views the File menu  
**THEN** Save All is disabled  
**SO THAT** the user cannot invoke a no‑op save  
**AS MEASURED BY** `SaveAllCommand.CanExecute == false` while no open tab has `IsDirty == true`

## 7.5 Save All Re-Enables When Any Tab Becomes Dirty
**GIVEN** Save All is disabled because no tab is dirty  
**WHEN** the user edits any open tab, setting its `IsDirty` to `true`  
**THEN** Save All becomes enabled  
**SO THAT** the menu always reflects current save‑pending state  
**AS MEASURED BY** `SaveAllCommand.CanExecute == true` once any open tab has `IsDirty == true`

---

# 8. Error Interactions

## 8.1 Pipeline Failure
**GIVEN** execution is running  
**WHEN** `pipeline_failed` arrives  
**THEN** global error banner appears  
**SO THAT** the user sees the failure immediately  
**AS MEASURED BY** `ErrorBannerViewModel.IsVisible == true`

## 8.2 Inspector Error
**GIVEN** an inspector fails to render  
**WHEN** an inspector error occurs  
**THEN** InspectorErrorPanel appears  
**SO THAT** the user sees the failure context  
**AS MEASURED BY** `InspectorErrorViewModel.Message != null`

## 8.3 WebSocket Disconnect
**GIVEN** execution is running  
**WHEN** WebSocket disconnects  
**THEN** global error banner appears  
**SO THAT** the user sees transport failure  
**AS MEASURED BY** `HasErrors == true`

---

# 9. Settings Interactions

## 9.1 Invalid Settings Block Save
**GIVEN** the user enters invalid settings  
**WHEN** they click Save  
**THEN** Save is blocked  
**SO THAT** backend remains stable  
**AS MEASURED BY** `IsValid == false`

## 9.2 Valid Settings Restart Backend
**GIVEN** settings are valid  
**WHEN** the user clicks Save  
**THEN** backend restarts  
**SO THAT** new configuration applies  
**AS MEASURED BY** successful relaunch of `llx serve`

---

# 10. Logging Persistence

## 10.1 Default Location Write
**GIVEN** no custom `LogPath` is configured  
**WHEN** a `UiError` is added to any logged collection (`WorkspaceViewModel.Errors`, a tab's `PipelineExecutionViewModel.Errors`, `SettingsViewModel.Errors`)  
**THEN** an entry is appended to `%APPDATA%\LimelightX\Limelight-x-log.txt`  
**SO THAT** diagnostics are recoverable without a custom setup  
**AS MEASURED BY** the file's contents after the error occurs

## 10.2 Custom LogPath Write
**GIVEN** the user has configured a custom `LogPath`  
**WHEN** a `UiError` is logged  
**THEN** the entry is appended to `<LogPath>\Limelight-x-log.txt` instead of the default location  
**SO THAT** the user's chosen log directory is honored  
**AS MEASURED BY** the file's contents at `<LogPath>`, and the absence of any new entry at the default location

## 10.3 Append Across Sessions
**GIVEN** the log file already contains entries from a previous session  
**WHEN** the app restarts and a new error is logged  
**THEN** both the prior and the new entries are present in the file  
**SO THAT** diagnostic history survives restarts  
**AS MEASURED BY** the file never being truncated on startup

## 10.4 Write Failure Does Not Block The Original Error
**GIVEN** the log directory is unwritable  
**WHEN** a `UiError` occurs  
**THEN** the app does not crash and no additional user-facing error is raised for the failed write  
**SO THAT** a logging problem never masks or blocks the real error  
**AS MEASURED BY** the original error still appearing through its normal UI surface (banner/inline/inspector)

## 10.5 LogPath Change Redirects Immediately
**GIVEN** logging is currently active at the default location  
**WHEN** the user saves a new `LogPath` in Settings  
**THEN** subsequent entries are appended to the new location  
**SO THAT** the user doesn't need to restart the app for a log-path change to take effect  
**AS MEASURED BY** no further entries appearing at the old location after the save, and new entries appearing at `<new LogPath>\Limelight-x-log.txt`

---

# 11. Correlation‑ID Interactions

## 11.1 Ignore Mismatched Events
**GIVEN** active correlation ID = `abc-123`  
**WHEN** an event arrives with `xyz-999`  
**THEN** UI ignores the event  
**SO THAT** no cross‑execution contamination occurs  
**AS MEASURED BY** unchanged inspector state

## 11.2 Reset on New Execution
**GIVEN** previous execution completed, with some panels left expanded  
**WHEN** new `pipeline_started` arrives  
**THEN** all inspector ViewModels clear their content and all six panels collapse  
**SO THAT** the UI begins a fresh execution with the same closed→auto-expand reveal every time  
**AS MEASURED BY** empty inspector content and `IsCollapsed == true` on all six inspector ViewModels

---

# 12. Non‑Goals

These interactions do **not** cover:

- parallel executions (per‑tab concurrent execution is a possible future extension)  
- queued executions  
- cancellation  
- plugin inspectors  
- nondeterministic animations  
- "stick to bottom unless scrolled up" auto-scroll tracking for Prompts/Model Outputs (auto-scroll is always unconditional, see §4.12–§4.13)  
- Tree‑sitter (or any client‑side parser) participating in validation or execution — it is advisory/decoration‑only, `/explain`/`/trace` remain the sole source of truth (§2.5–§2.15, `cnl-editor-architecture.md` §1, §5)  
- semantic (cross‑reference / normalization‑aware) completions or hover — both are syntactic only (§2.11, §2.13)  
- sending a Tree‑sitter CST, or anything derived from it, to `/src/api` or Rust by any channel

---

# Summary

These BDD scenarios define all deterministic user interactions in the Limelight‑X UI under the streaming API.  
They ensure predictable execution workflows, incremental inspector updates, deterministic layout resizing, strict navigation constraints, and robust error handling — all aligned with the Limelight‑X architecture.