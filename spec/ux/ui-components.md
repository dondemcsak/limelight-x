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

4. **Strict Single Execution**  
   - Execution components disable during pipeline execution.  
   - No parallel execution UI states.

---

# 2. Component Overview

The UI defines the following components:

- `NavigationBar`
- `Sidebar`
- `Editor`
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

---

# 3. Navigation Components

## 3.1 NavigationBar

### Responsibilities
- Provides top‑level navigation.
- Shows Home, Editor, Execution, Settings.

### Bindings
- `CurrentPage` → highlight active page  
- `NavigateHomeCommand`  
- `NavigateEditorCommand`  
- `NavigateExecutionCommand`  
- `NavigateSettingsCommand`

### Streaming Rules
- Navigation buttons disabled during execution (`IsRunning == true`).

---

## 3.2 Sidebar

### Responsibilities
- Provides persistent navigation.
- Mirrors NavigationBar behavior.

### Bindings
- Same as NavigationBar.

### Streaming Rules
- Sidebar navigation disabled during execution.

---

# 4. Editor Components

## 4.1 Editor

### Responsibilities
- Displays CNL text.
- Shows inline validation errors.
- Provides Run/Explain/Trace buttons.

### Bindings
- `SourceText`  
- `SyntaxErrors`  
- `RunCommand`  
- `ExplainCommand`  
- `TraceCommand`  
- `PipelineExecutionViewModel.IsRunning` → disable buttons

### Streaming Rules
- Editor remains visible only on Editor Page.  
- Inline errors come from `/explain`'s streamed event sequence (`pipeline_started` → `raw_ast_generated` → `normalized_ast_generated`), the same as any other execution — see `ui-viewmodels.md` §5 Live Validation.

---

# 5. Execution Components

## 5.1 ExecutionTimeline

### Responsibilities
- Displays vertical pipeline timeline.
- Highlights active stage based on event type.

### Bindings
- `PipelineEvents`  
- `IsRunning`

### Streaming Rules
- Timeline updates incrementally as events arrive.  
- Active stage highlights deterministically.

---

# 6. Inspector Components

Each inspector is a collapsible panel that appears when its event arrives.

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
- Inspector clears on `pipeline_started`.

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
- Appears on `ir_generated`.

---

## 6.5 PromptPanel

### Responsibilities
- Displays prompts sent to the model.

### Bindings
- `PromptViewModel.Prompts`  
- `IsCollapsed`

### Streaming Rules
- Appears on `prompts_generated`.

---

## 6.6 ModelOutputPanel

### Responsibilities
- Displays model outputs.

### Bindings
- `ModelOutputViewModel.Outputs`  
- `IsCollapsed`

### Streaming Rules
- Appears on `model_outputs_generated`.

---

## 6.7 FinalResultPanel

### Responsibilities
- Displays final result text.

### Bindings
- `FinalResultViewModel.ResultText`  
- `FinalResultViewModel.ContentType`  
- `IsCollapsed`

### Streaming Rules
- Appears on `final_result_ready`.

---

# 7. Error Components

## 7.1 ErrorBanner

### Responsibilities
- Displays global errors.
- Expands to show full error list.

### Bindings
- `ErrorBannerViewModel.IsVisible`  
- `ErrorBannerViewModel.Errors`  
- `DismissCommand`

### Streaming Rules
- Appears on:
  - `pipeline_failed`
  - WebSocket disconnect
  - malformed event

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
- Displays backend configuration fields.
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
- Settings Page is inaccessible during execution.

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
- correct execution lock behavior  

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
- multi‑file project components  
- plugin inspector components  

---

# Summary

Limelight‑X UI components are deterministic, MVVM‑pure, and fully aligned with the streaming API.  
Inspector panels update incrementally as events arrive, the Execution Page reflects real‑time pipeline progress, and all components derive their behavior exclusively from ViewModels.