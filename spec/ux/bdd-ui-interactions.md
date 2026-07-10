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

## 2.2 Live Validation
**GIVEN** the user modifies CNL text  
**WHEN** the editor triggers `/explain` validation  
**THEN** syntax errors appear inline  
**SO THAT** the user sees grammar issues immediately  
**AS MEASURED BY** updated `SyntaxErrors` and margin markers

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

## 2.5–2.15 Tree‑sitter Editor Decoration

> **Status: FINAL.** Authored to close the gap identified when reviewing `spec/cnl-editor-architecture.md`: no BDD source existed anywhere for syntax highlighting migration, folding, hover, or completion before this addition. Cross-checked against `ui-viewmodels.md` §6 and `ui-components.md` §4.2 (which were extended with matching `CompletionItems`/`HoverInfo`/`QuickFixes` state) and, in this pass, against `FoldRegions`/`LocalDiagnostics` (§2.7–§2.9) — no drift found; `HoverInfo` is confirmed nullable (`null` = no hover), `CompletionItems` is confirmed to be the same `ObservableCollection<CompletionItem>` type, and `FoldRegions`/`LocalDiagnostics` are confirmed to be `ObservableCollection<FoldRegion>`/`ObservableCollection<LocalDiagnostic>` on `EditorViewModel`. These 11 scenarios are the executable spec for the tests in `ui/tests/Edit/EditorViewModelTests.cs` and `ui/tests/Intellisense/` — one test per scenario, per `CLAUDE.md` §6.
>
> Scope premise (per `cnl-editor-architecture.md` §1, §5, as clarified): Tree‑sitter is client‑side‑only editor decoration. It never calls, and is never called by, `/src/api` or Rust. It does not participate in validation (`SyntaxErrors`) or execution. Everything below is local computation, keyed off the CST that `spec/parsing/grammer-js.md` produces from `SourceText`.

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
**AS MEASURED BY** tokens before/after the error region retaining their `TokenKind` color; the error region itself represented as one entry in `EditorViewModel.LocalDiagnostics` (§2.8), not a validation‑error style

## 2.8 Local Error Squiggles Never Replace `/explain` Validation
**GIVEN** the user types invalid CNL text  
**WHEN** Tree‑sitter's next in‑process reparse produces an `ERROR` node, arriving before the debounced `/explain` call returns  
**THEN** `EditorViewModel.LocalDiagnostics` gains an entry for the `ERROR` node's span immediately, but `EditorViewModel.SyntaxErrors` is unchanged by it  
**SO THAT** the user gets fast visual feedback without Tree‑sitter becoming a second, possibly‑conflicting source of truth for validation state  
**AS MEASURED BY** `SyntaxErrors` changing only in response to `/explain`'s streamed events (§2.2), never in response to a Tree‑sitter reparse alone — confirmed by a case where Tree‑sitter shows no error node (`LocalDiagnostics` empty) but `/explain` still returns one (`SyntaxErrors` non-empty), and vice versa, with `SyntaxErrors` reflecting only `/explain`'s answer in both cases and `LocalDiagnostics` reflecting only the current parse's `ERROR`/`MISSING` nodes

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
**WHEN** a `UiError` is added to any logged collection (`WorkspaceViewModel.Errors`, a tab's `EditorViewModel.ValidationErrors`, a tab's `PipelineExecutionViewModel.Errors`, `SettingsViewModel.Errors`)  
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