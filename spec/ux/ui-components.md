# UI Components (Streaming Edition)

## Purpose
This document defines all UI components used by the Limelight‑X UI.  
It specifies their responsibilities, structure, bindings, and deterministic behavior under the **event‑streaming API**.

This specification is authoritative.  
All implementation must follow this component model exactly.

The UI is MVVM‑pure:  
- Views contain no logic.  
- Components are declarative.  
- All behavior is driven by ViewModels and streaming events.

---

# 1. Architectural Principles

1. **Deterministic Rendering**  
   - Components must render deterministically based on ViewModel state.  
   - No hidden transitions or nondeterministic animations.

2. **MVVM Purity**  
   - Components contain no logic.  
   - All state comes from ViewModels.  
   - All commands come from ViewModels.

3. **Streaming‑Aware Components**  
   - Components update incrementally as events arrive.  
   - Inspectors appear only when their corresponding event arrives.

4. **App‑Wide Single Execution**  
   - Execution components (Run/Explain buttons, Settings gear) disable app‑wide during any tab's pipeline execution.  
   - No parallel execution UI states.

---

# 2. Component Overview

The UI defines the following components:

- `MenuBar`
- `FileTreeView`
- `TabStrip`
- `TabContentHost`
- `CnlTabView`
- `SplitterControl`
- `Editor`
- `PlainTextEditor`
- `LoadingIndicator`
- `InspectorPanel`
- `RawAstPanel`
- `NormalizedAstPanel`
- `IrPanel`
- `PromptPanel`
- `ModelOutputPanel`
- `FinalResultPanel`
- `ErrorBanner`
- `SettingsForm`
- `AboutContent`

Each component is declarative and state‑derived.

`NavigationBar` and `Sidebar` (the previous 4‑button nav components) are retired entirely, replaced by `FileTreeView` and `TabStrip` below.

---

# 3. Workspace Shell Components

## 3.1 FileTreeView

### Responsibilities
- Displays the directory tree of the currently open root folder.
- Expands/collapses folders.
- Opens (or focuses) a tab when a file is clicked.

### Bindings
- `FileTreeViewModel.Nodes` (recursive)
- `FileTreeViewModel.SelectedNode`
- `WorkspaceViewModel.OpenOrFocusTabCommand`
- `FileTreeNodeViewModel.IsExpanded` (per node)

### Streaming Rules
- Never disabled by execution state — folder browsing is always available (`ui-routing-navigation.md` §7).

---

## 3.2 TabStrip

### Responsibilities
- Shows one tab per open file (`WorkspaceViewModel.OpenTabs`).
- Highlights the active tab (`WorkspaceViewModel.ActiveTab`).
- Shows a dirty‑state indicator per tab (`TabViewModel.IsDirty`).
- Provides a close button per tab.

### Bindings
- `WorkspaceViewModel.OpenTabs`
- `WorkspaceViewModel.ActiveTab`
- `TabViewModel.Header`
- `TabViewModel.IsDirty`
- `TabViewModel.CloseCommand`

### Streaming Rules
- Never disabled by execution state — switching, opening, and closing tabs is always available (`ui-routing-navigation.md` §7).
- Closing a dirty tab triggers the unsaved‑changes confirmation dialog before the tab is removed.

---

## 3.3 TabContentHost

### Responsibilities
- Renders the active tab's content: `CnlTabView` for a `CnlTabViewModel`, `PlainTextEditor` for a `PlainTextTabViewModel`, or a welcome/empty state when there are no open tabs.

### Bindings
- `WorkspaceViewModel.ActiveTab`

### Streaming Rules
- Switching tabs never interrupts another tab's in‑flight execution — the previously active tab continues streaming in the background.

---

## 3.4 Settings Gear Icon

### Responsibilities
- Persistent icon (title/activity‑bar area) that opens the Settings modal.

### Bindings
- `WorkspaceViewModel.OpenSettingsCommand`
- `IExecutionLockService.IsAnyExecutionRunning` → disable

### Streaming Rules
- Disabled while any tab's execution is in flight (`ui-routing-navigation.md` §7).
- File > Settings (§3.5) is a second entry point bound to this same `OpenSettingsCommand` and the same disable condition — this icon is not removed or replaced by the menu item.

---

## 3.5 MenuBar

### Responsibilities
- Custom in‑window menu bar rendered in the title‑bar row, styled with existing theme tokens (`ui-styling-theming.md` §4.9) — not a native OS menu bar.
- Exposes exactly two top‑level menus, **File** and **Help**, with no other top‑level menus.

### Structure
```
File                          Help
├─ New LLX File      Ctrl+N   └─ About
├─ New TXT File
├─ Open File          Ctrl+O
├─ Open Folder   Ctrl+K,Ctrl+O
├─ ───────────────────────
├─ Save               Ctrl+S
├─ Save As      Ctrl+Shift+S
├─ Save All         Ctrl+K,S
├─ ───────────────────────
└─ Settings              Ctrl+,
```

### Bindings
- File > New LLX File → `WorkspaceViewModel.NewLlxFileCommand`
- File > New TXT File → `WorkspaceViewModel.NewTxtFileCommand`
- File > Open File → `WorkspaceViewModel.OpenFileCommand`
- File > Open Folder → `WorkspaceViewModel.OpenFolderCommand`
- File > Save → `WorkspaceViewModel.SaveCommand`
- File > Save As → `WorkspaceViewModel.SaveAsCommand`
- File > Save All → `WorkspaceViewModel.SaveAllCommand`
- File > Settings → `WorkspaceViewModel.OpenSettingsCommand` (same command as §3.4)
- Help > About → `WorkspaceViewModel.OpenAboutCommand`

### Rules
- New LLX File, New TXT File, Open File, and Open Folder are always enabled — they do not require a folder to already be open.
- Save and Save As are enabled only when there is an `ActiveTab`.
- Save All is enabled only when at least one open tab has `IsDirty == true`.
- Settings is disabled while `IExecutionLockService.IsAnyExecutionRunning == true` (same gate as §3.4).
- About is never disabled by execution state (`ui-routing-navigation.md` §7.1).

### Streaming Rules
- None — the MenuBar itself has no relationship to the streaming pipeline; individual item enablement derives from tab/dirty/execution‑lock state as described above.

---

# 4. Tab Content Components

## 4.1 CnlTabView

### Responsibilities
- Composite view for an open `.llx` tab: CNL editor on top, execution panel on the bottom.
- Hosts exactly two buttons above the editor: **Run** and **Explain**.

### Structure
```
[ Run ] [ Explain ]
--------------------
Editor (CnlEditor)            ┐
                               ├ top half, editor/panel split ratio
--------------------          ┘
SplitterControl (§4.5)   ← draggable, resizes top/bottom halves
--------------------          ┐
LoadingIndicator (§4.4)       │
--------------------          ├ bottom half
Execution Panel                │
  (all six Inspector Panels,  │
   §5, always rendered,       │
   initially collapsed)       ┘
```

### Bindings
- `CnlTabViewModel.Editor` (§4.2)
- `CnlTabViewModel.PipelineExecution` (drives §4.4 and §5 below, scoped to this tab)
- `CnlTabViewModel.EditorPaneRatio` (§4.5 `SplitterControl`, `ui-viewmodels.md` §12)

### Streaming Rules
- Both halves belong to the same tab and share its `PipelineExecutionViewModel` instance — there is no separate "Execution Page" to navigate to.
- The `LoadingIndicator` (§4.4) between the editor and the execution panel shows while `PipelineExecution.IsRunning` is `true` (from `pipeline_started` until this tab's terminal event) and is hidden otherwise. It is scoped to this tab only — it does not appear in other open tabs even though their Run/Explain buttons are also disabled by the app-wide lock during this time (see `ui-viewmodels.md` §7, `bdd-ui-navigation.md` §7.6).
- The execution panel always renders all six inspector panels (§5), each starting collapsed — it is never empty or absent, unlike the editor/`SplitterControl`/`LoadingIndicator` which are structurally fixed but the panels' *content* is incremental.

---

## 4.2 Editor (CNL Editor)

### Responsibilities
- Displays CNL text for the owning `.llx` tab.
- Shows inline validation errors.
- Shows Tree‑sitter‑driven syntax highlighting, folding, a completion list, and hover tooltips (`spec/cnl-editor-architecture.md`, `spec/ux/ui-editor-services-guide.md`).
- Provides Run/Explain buttons (rendered by `CnlTabView`, §4.1, bound to this ViewModel's commands).

### Bindings
- `SourceText`  
- `LocalDiagnostics` — advisory, Tree‑sitter‑sourced; the editor's only error-shaped state (`bdd-ui-interactions.md` §2.2, §2.8) — rendered by `LocalDiagnosticsRenderer` as a squiggly underline + margin marker (`bdd-ui-interactions.md` §2.16), same visual shape as `PipelineExecutionViewModel.ErrorBanner`'s styling (`ui-error-handling.md` §10.3) but never merged into it  
- `CompletionItems`, `SelectCompletionItemCommand` — completion list popup, positioned at the cursor  
- `HoverInfo` — hover tooltip; not shown when `null`; sourced from a `LocalDiagnostics` match first, falling back to grammar‑role hover (`bdd-ui-interactions.md` §2.17)  
- `GhostSuggestion` — rendered inline by `GhostTextElementGenerator` as non‑editable, semi‑transparent text at the caret when set; committed to real text by `Tab` via `ApplyQuickFixCommand` (`bdd-ui-interactions.md` §2.18–§2.19)  
- `RunCommand` (invokes `/trace`)
- `ExplainCommand` (invokes `/explain`)
- `IExecutionLockService.IsAnyExecutionRunning` → disable both buttons (does **not** disable completion/hover/highlighting/folding — those are local, per Streaming Rules below)

### Streaming Rules
- There is no `TraceCommand` binding; the Trace trigger is removed entirely.
- This component never triggers a backend call on its own. Authoritative errors only ever arrive as a result of an explicit Run/Explain click and surface through `PipelineExecutionViewModel.ErrorBanner` in the execution panel (`ui-viewmodels.md` §7), not through this component (`bdd-ui-interactions.md` §2.2).
- Syntax highlighting, folding, completions, and hover are computed entirely client-side by Tree‑sitter and are **not** streaming-driven and **not** gated by `IExecutionLockService` — see `ui-viewmodels.md` §6 "IntelliSense (Tree‑sitter)". Local Tree‑sitter error nodes render a squiggly underline + margin marker, visually matching but data‑model‑separate from `PipelineExecutionViewModel.ErrorBanner`'s styling (`ui-error-handling.md` §10.3); they never replace or delay it.

### Sub‑Components
- **`LocalDiagnosticsRenderer`** (`ui/components/LocalDiagnosticsRenderer.cs`) — an `IBackgroundRenderer` drawing a zig‑zag squiggle stroke (not a filled wash) plus a margin marker glyph for each `LocalDiagnostics` entry's span, in `SyntaxErrorBrush`.
- **`GhostTextElementGenerator`** (`ui/components/GhostTextElementGenerator.cs`) — a `VisualLineElementGenerator` that injects a single non‑editable, low‑opacity text element at `GhostSuggestion.InsertionByte` when set, leaving surrounding real content unaffected; `null` when there is no active `GhostSuggestion`.

### Layout Rules
- Occupies the top half of the tab's content area (below the Run/Explain buttons), sized by `CnlTabViewModel.EditorPaneRatio` (§4.1, §4.5).
- Displays a vertical scrollbar on the right edge when `SourceText` exceeds the available height of its current split allocation.

---

## 4.3 PlainTextEditor

### Responsibilities
- Displays and edits plain text file content for a non‑`.llx` tab.
- No CNL syntax highlighting, no validation, no execution controls.

### Bindings
- `PlainTextEditorViewModel.Text`
- `PlainTextEditorViewModel.CursorPosition`
- `PlainTextEditorViewModel.IsDirty`

### Streaming Rules
- None — this component has no relationship to the streaming pipeline.

---

## 4.4 LoadingIndicator

### Responsibilities
- A small, generic reusable control: an indeterminate spinner plus a text label.
- Shows or hides as a whole based on a single bound flag — no independent state of its own.

### Bindings
- `IsLoading : bool`
- `Text : string`

### Usages
- In `CnlTabView` (§4.1): bound to `PipelineExecutionViewModel.IsRunning`, `Text="Running..."`, positioned between the CNL editor and the execution panel.
- In the Settings modal (`ui-routing-navigation.md` §2, §8; see also `ui-components.md` §7.1): bound to `SettingsViewModel.IsApplying`, `Text="Applying settings..."`.
- In `CnlTabView` (§4.1), a second instance: bound to `PipelineExecutionViewModel.IsAwaitingModelOutput`, `Text="Waiting for model response..."`, positioned between `PromptPanel` (§5.5) and `ModelOutputPanel` (§5.6) — gives visible feedback while waiting for a model call's output, since `ModelOutputPanel` itself stays collapsed until its first `model_output_generated` event (§5.6).

### Streaming Rules
- Purely a display of its bound `IsLoading` flag — has no streaming logic of its own; each caller is responsible for deriving `IsLoading` from its own state (this tab's `IsRunning` for `CnlTabView`, `IsApplying` for the Settings modal, `IsAwaitingModelOutput` for the second `CnlTabView` instance).

---

## 4.5 SplitterControl

### Responsibilities
- A reusable draggable resize handle placed between two adjacent regions.
- Used in two places: (a) between the Editor and the execution panel in `CnlTabView` (§4.1), reallocating the tab's top/bottom split; (b) on the lower edge of each inspector panel (§5.1), reallocating that panel's height against the panel(s) below it.

### Bindings
- `Orientation` (horizontal divider for a vertical split, as used in both usages here)
- Adjacent-region size properties: `CnlTabViewModel.EditorPaneRatio` for usage (a); the relevant `InspectorPanelViewModel.Height` pair for usage (b) — see `ui-viewmodels.md` §12

### Streaming Rules
- None — purely a layout control, never gated by execution state and never itself a source or consumer of streaming events.

### Rules
- Dragging never changes collapsed state or content of the regions it resizes.
- Resizing usage (b) trades height only with the immediate next-neighbor panel below (classic two-panel splitter behavior) — panels further down are unaffected in size and simply reposition; the overall bottom half's scrollable height can grow or shrink as a result.

---

# 5. Inspector Components

Each inspector is a collapsible panel, scoped to a single `.llx` tab, always rendered from the moment the tab opens (starting `IsCollapsed = true`), that auto-expands when its first relevant event arrives.

## 5.1 InspectorPanel (Base Component)

### Responsibilities
- Provides shared structure for all inspectors.
- Handles collapse/expand behavior.
- Handles resize behavior (this panel's height) and internal content scrolling.

### Bindings
- `IsCollapsed` (default `true` — panel starts closed when the tab opens, see `ui-viewmodels.md` §11)
- `Title`  
- `HasErrors`  
- `ErrorMessage`  
- `Height` (this panel's current expanded height, adjusted via its `SplitterControl` handle, §4.5)

### Layout Rules
- Always rendered in the bottom half of the tab, from the moment the tab opens — never inserted or removed from the layout, regardless of `IsCollapsed`.
- Has an implementation-chosen default `Height` when expanded (exact value left to implementation, not fixed by this spec — see `ui-architecture.md` §7 Resize Behavior).
- Exposes a `SplitterControl` (§4.5) on its lower edge; dragging it trades height only with its immediate next-neighbor panel below (classic two-panel splitter behavior) — other panels are unaffected in size and simply reposition as the stack reflows.
- Its content area scrolls vertically (scrollbar on the right edge) when content exceeds the panel's current `Height`.

### Streaming Rules
- Inspector auto-expands (`IsCollapsed` → `false`) only when its ViewModel receives its first relevant data for the current execution.  
- Inspector clears its content and resets `IsCollapsed` to `true` on `pipeline_started` (for this tab) — see `ui-viewmodels.md` §7.

---

## 5.2 RawAstPanel

### Responsibilities
- Displays raw AST nodes.

### Bindings
- `RawAstViewModel.AstNodes`  
- `IsCollapsed`

### Streaming Rules
- Auto-expands (`IsCollapsed` → `false`) on `raw_ast_generated`. The panel itself is always rendered, starting collapsed (§5.1).

---

## 5.3 NormalizedAstPanel

### Responsibilities
- Displays normalized AST nodes.

### Bindings
- `NormalizedAstViewModel.NormalizedNodes`  
- `IsCollapsed`

### Streaming Rules
- Auto-expands (`IsCollapsed` → `false`) on `normalized_ast_generated`. The panel itself is always rendered, starting collapsed (§5.1).

---

## 5.4 IrPanel

### Responsibilities
- Displays IR operations.

### Bindings
- `IrViewModel.Operations`  
- `IsCollapsed`

### Streaming Rules
- Auto-expands (`IsCollapsed` → `false`) on `ir_generated`. The panel itself is always rendered, starting collapsed (§5.1). Reachable only via Run (`/trace`); Explain (`/explain`) never populates this panel, which simply remains collapsed for an Explain execution.

---

## 5.5 PromptPanel

### Responsibilities
- Displays prompts sent to the model.

### Bindings
- `PromptViewModel.Prompts`  
- `IsCollapsed`

### Streaming Rules
- Auto-expands (`IsCollapsed` → `false`) on the first `prompt_generated` event; subsequent `prompt_generated` events append to `PromptViewModel.Prompts` without re-collapsing or re-expanding the panel. The panel itself is always rendered, starting collapsed (§5.1), and simply stays collapsed if the trace has zero model-calling operations. Reachable only via Run.
- On every appended entry (including the one that triggers the first auto-expand), the panel scrolls its content to reveal the newest entry, unconditionally — no scroll-position tracking (see `ui-architecture.md` §7 Auto-Scroll Behavior).

---

## 5.6 ModelOutputPanel

### Responsibilities
- Displays model outputs.
- Each item shows a `{Metadata.TokenUsage} tokens · {Metadata.LatencyMs} ms` caption below the rendered output, so the user can see the model call's real cost/duration rather than guessing whether a wait is the app or the model.

### Bindings
- `ModelOutputViewModel.Outputs`  
- `IsCollapsed`

### Streaming Rules
- Auto-expands (`IsCollapsed` → `false`) on the first `model_output_generated` event; subsequent `model_output_generated` events append to `ModelOutputViewModel.Outputs` without re-collapsing or re-expanding the panel. The panel itself is always rendered, starting collapsed (§5.1), and simply stays collapsed if the trace has zero model-calling operations. Reachable only via Run.
- On every appended entry (including the one that triggers the first auto-expand), the panel scrolls its content to reveal the newest entry, unconditionally — no scroll-position tracking (see `ui-architecture.md` §7 Auto-Scroll Behavior).

---

## 5.7 FinalResultPanel

### Responsibilities
- Displays final result text.

### Bindings
- `FinalResultViewModel.ResultText`  
- `FinalResultViewModel.ContentType`  
- `IsCollapsed`

### Streaming Rules
- Auto-expands (`IsCollapsed` → `false`) on `final_result_ready`. The panel itself is always rendered, starting collapsed (§5.1). Reachable only via Run.

---

# 6. Error Components

## 6.1 ErrorBanner

### Responsibilities
- Displays errors for a single tab (or, for Settings‑relaunch failures, within the Settings modal — see `ui-error-handling.md` §7.5).
- Expands to show full error list.

### Bindings
- `ErrorBannerViewModel.IsVisible`  
- `ErrorBannerViewModel.Errors`  
- `DismissCommand`

### Streaming Rules
- Appears on:
  - `pipeline_failed` (this tab's execution)
  - WebSocket disconnect (this tab's execution)
  - malformed event (this tab's execution)
- Scoped to its owning tab — switching tabs never shows or hides another tab's banner.

---

## 6.2 InspectorErrorPanel

### Responsibilities
- Displays inspector‑specific errors.

### Bindings
- `InspectorErrorViewModel.Message`  
- `InspectorErrorViewModel.Severity`  
- `IsCollapsed`

### Streaming Rules
- Appears when inspector data cannot be rendered.

---

# 7. Settings & About Components

## 7.1 SettingsForm

### Responsibilities
- Displays backend configuration fields, inside the Settings modal (`ui-routing-navigation.md` §2, §8).
- Validates input.
- Applies settings.

### Bindings
- `BackendPort`  
- `ApiKey`  
- `LogPath`  
- `EnvironmentProfile`  
- `IsValid`  
- `SaveSettingsCommand`

### Streaming Rules
- The Settings gear that opens this modal is disabled while `IExecutionLockService.IsAnyExecutionRunning == true` (§3.4).
- While settings are being applied, a `LoadingIndicator` (§4.4) is shown, bound to `IsApplying`, `Text="Applying settings..."`.

---

## 7.2 AboutContent

### Responsibilities
- Displays read‑only project information inside the About modal (`ui-routing-navigation.md` §2, §9): app name, project description, version string, and a link to the GitHub repository (`https://github.com/dondemcsak/limelight-x`).
- Provides a close action.

### Bindings
- `AboutViewModel.AppName`  
- `AboutViewModel.Description`  
- `AboutViewModel.Version`  
- `AboutViewModel.GitHubUrl`  
- `AboutViewModel.CloseCommand`

### Streaming Rules
- None — never gated by execution state, in explicit contrast with §7.1 SettingsForm (About has no backend side effects).

---

# 8. Component Determinism Rules

### Allowed Behavior
- collapse/expand  
- deterministic rendering  
- incremental updates  
- stable ordering  
- panel/pane resizing via `SplitterControl` (§4.5)  
- deterministic auto-scroll to the newest entry (PromptPanel, ModelOutputPanel; §5.5–§5.6)  

### Forbidden Behavior
- nondeterministic animations  
- hidden transitions  
- buffering or reordering events  
- implicit state machines  
- pipeline logic inside components  

---

# 9. Component Testing Requirements

Each component must be tested for:

- deterministic rendering  
- correct bindings  
- correct collapse/expand behavior  
- correct incremental updates  
- correct error rendering  
- correct execution lock behavior (app‑wide disable/enable, scoped per‑tab results)  
- correct MenuBar bindings and item enablement (New/Open always enabled, Save/Save As require an active tab, Save All requires a dirty tab, Settings gated by execution lock, About never gated)  
- correct About modal rendering (app name, description, version, GitHub link)  
- correct editor/panel split resize via `SplitterControl` (§4.5), including the editor's internal scrollbar appearing only when content overflows its current allocation  
- correct panel accordion resize (dragging one panel's `SplitterControl` trades height only with its immediate next-neighbor panel below; other panels reposition without resizing)  
- correct auto-expand-on-first-event behavior for all six inspector panels, and correct re-collapse of all six on `pipeline_started`  
- correct auto-scroll-to-newest-entry behavior for PromptPanel and ModelOutputPanel on every appended entry

---

# 10. Non‑Goals

Components do **not** support:

- custom inspectors  
- plugin components  
- nondeterministic transitions  
- parallel execution UI  
- pipeline reconstruction  
- dynamic component injection  

---

# 11. Future Extensions

Potential enhancements:

- animated inspector transitions (deterministic only)  
- richer IR visualization components  
- plugin inspector components  
- per‑tab independent execution UI (concurrent tab executions)

---

# Summary

Limelight‑X UI components are deterministic, MVVM‑pure, and fully aligned with the streaming API.  
A folder tree and tab strip replace the old navigation Sidebar; each open `.llx` tab hosts its own editor and execution panel, updating incrementally as events arrive for that tab, and all components derive their behavior exclusively from ViewModels.  
A custom‑themed MenuBar (File, Help) provides discoverable entry points for file operations and the About modal, alongside the existing Settings gear icon.
