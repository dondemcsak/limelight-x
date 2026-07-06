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

- `FileTreeView`
- `TabStrip`
- `TabContentHost`
- `CnlTabView`
- `Editor`
- `PlainTextEditor`
- `ExecutionTimeline`
- `InspectorPanel`
- `RawAstPanel`
- `NormalizedAstPanel`
- `IrPanel`
- `PromptPanel`
- `ModelOutputPanel`
- `FinalResultPanel`
- `ErrorBanner`
- `SettingsForm`

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
Editor (CnlEditor)
--------------------
Execution Panel (ExecutionTimeline + Inspector Panels, §5–§6)
```

### Bindings
- `CnlTabViewModel.Editor` (§4.2)
- `CnlTabViewModel.PipelineExecution` (drives §5–§6 below, scoped to this tab)

### Streaming Rules
- Both halves belong to the same tab and share its `PipelineExecutionViewModel` instance — there is no separate "Execution Page" to navigate to.

---

## 4.2 Editor (CNL Editor)

### Responsibilities
- Displays CNL text for the owning `.llx` tab.
- Shows inline validation errors.
- Provides Run/Explain buttons (rendered by `CnlTabView`, §4.1, bound to this ViewModel's commands).

### Bindings
- `SourceText`  
- `SyntaxErrors`  
- `RunCommand` (invokes `/trace`)
- `ExplainCommand` (invokes `/explain`)
- `IExecutionLockService.IsAnyExecutionRunning` → disable both buttons

### Streaming Rules
- There is no `TraceCommand` binding; the Trace trigger is removed entirely.
- Inline errors come from `/explain`'s streamed event sequence (`pipeline_started` → `raw_ast_generated` → `normalized_ast_generated`), the same as any other execution — see `ui-viewmodels.md` §6 Live Validation.

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

# 5. Execution Components

## 5.1 ExecutionTimeline

### Responsibilities
- Displays vertical pipeline timeline for the owning `.llx` tab.
- Highlights active stage based on event type.

### Bindings
- `PipelineExecutionViewModel.PipelineEvents` (this tab's instance)
- `PipelineExecutionViewModel.IsRunning` (this tab's instance)

### Streaming Rules
- Timeline updates incrementally as events arrive for this tab only.  
- Active stage highlights deterministically.

---

# 6. Inspector Components

Each inspector is a collapsible panel, scoped to a single `.llx` tab, that appears when its event arrives.

## 6.1 InspectorPanel (Base Component)

### Responsibilities
- Provides shared structure for all inspectors.
- Handles collapse/expand behavior.

### Bindings
- `IsCollapsed`  
- `Title`  
- `HasErrors`  
- `ErrorMessage`  

### Streaming Rules
- Inspector appears only when its ViewModel receives data.  
- Inspector clears on `pipeline_started` (for this tab).

---

## 6.2 RawAstPanel

### Responsibilities
- Displays raw AST nodes.

### Bindings
- `RawAstViewModel.AstNodes`  
- `IsCollapsed`

### Streaming Rules
- Appears on `raw_ast_generated`.

---

## 6.3 NormalizedAstPanel

### Responsibilities
- Displays normalized AST nodes.

### Bindings
- `NormalizedAstViewModel.NormalizedNodes`  
- `IsCollapsed`

### Streaming Rules
- Appears on `normalized_ast_generated`.

---

## 6.4 IrPanel

### Responsibilities
- Displays IR operations.

### Bindings
- `IrViewModel.Operations`  
- `IsCollapsed`

### Streaming Rules
- Appears on `ir_generated`. Reachable only via Run (`/trace`); Explain (`/explain`) never populates this panel.

---

## 6.5 PromptPanel

### Responsibilities
- Displays prompts sent to the model.

### Bindings
- `PromptViewModel.Prompts`  
- `IsCollapsed`

### Streaming Rules
- Appears on `prompts_generated`. Reachable only via Run.

---

## 6.6 ModelOutputPanel

### Responsibilities
- Displays model outputs.

### Bindings
- `ModelOutputViewModel.Outputs`  
- `IsCollapsed`

### Streaming Rules
- Appears on `model_outputs_generated`. Reachable only via Run.

---

## 6.7 FinalResultPanel

### Responsibilities
- Displays final result text.

### Bindings
- `FinalResultViewModel.ResultText`  
- `FinalResultViewModel.ContentType`  
- `IsCollapsed`

### Streaming Rules
- Appears on `final_result_ready`. Reachable only via Run.

---

# 7. Error Components

## 7.1 ErrorBanner

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

## 7.2 InspectorErrorPanel

### Responsibilities
- Displays inspector‑specific errors.

### Bindings
- `InspectorErrorViewModel.Message`  
- `InspectorErrorViewModel.Severity`  
- `IsCollapsed`

### Streaming Rules
- Appears when inspector data cannot be rendered.

---

# 8. Settings Components

## 8.1 SettingsForm

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

---

# 9. Component Determinism Rules

### Allowed Behavior
- collapse/expand  
- deterministic rendering  
- incremental updates  
- stable ordering  

### Forbidden Behavior
- nondeterministic animations  
- hidden transitions  
- buffering or reordering events  
- implicit state machines  
- pipeline logic inside components  

---

# 10. Component Testing Requirements

Each component must be tested for:

- deterministic rendering  
- correct bindings  
- correct collapse/expand behavior  
- correct incremental updates  
- correct error rendering  
- correct execution lock behavior (app‑wide disable/enable, scoped per‑tab results)

---

# 11. Non‑Goals

Components do **not** support:

- custom inspectors  
- plugin components  
- nondeterministic transitions  
- parallel execution UI  
- pipeline reconstruction  
- dynamic component injection  

---

# 12. Future Extensions

Potential enhancements:

- animated inspector transitions (deterministic only)  
- richer IR visualization components  
- plugin inspector components  
- per‑tab independent execution UI (concurrent tab executions)

---

# Summary

Limelight‑X UI components are deterministic, MVVM‑pure, and fully aligned with the streaming API.  
A folder tree and tab strip replace the old navigation Sidebar; each open `.llx` tab hosts its own editor and execution panel, updating incrementally as events arrive for that tab, and all components derive their behavior exclusively from ViewModels.
