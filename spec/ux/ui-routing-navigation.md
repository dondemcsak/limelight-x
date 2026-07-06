# UI Routing & Navigation (Streaming Edition)

## Purpose
This document defines the complete routing and navigation model for the Limelight‑X UI.  
It specifies page transitions, navigation rules, execution‑driven routing, error routing, and deterministic behavior under the new **event‑streaming API**.

This specification is authoritative.  
All implementation must follow this routing model exactly.

---

# 1. Architectural Principles

1. **Deterministic Navigation**  
   - All navigation is explicit.  
   - No hidden transitions.  
   - No nondeterministic animations.

2. **MVVM‑Driven Routing**  
   - Navigation is controlled exclusively by `NavigationViewModel`.  
   - Views contain no routing logic.

3. **Execution‑Driven Transitions**  
   - Starting a pipeline execution automatically navigates to the Execution Page.  
   - Streaming events update the Execution Page in real time.

4. **Strict Single Execution**  
   - Only one pipeline execution may be active at a time.  
   - Navigation must not allow leaving the Execution Page while a pipeline is running.

5. **Error‑Driven Routing**  
   - Pipeline errors (`pipeline_failed`) keep the user on the Execution Page.  
   - Global error banner appears immediately.

---

# 2. Pages

The UI defines four pages:

1. **Home Page**  
2. **Editor Page**  
3. **Execution Page**  
4. **Settings Page**

Each page is represented by a View and a corresponding ViewModel.

---

# 3. NavigationViewModel

### Responsibilities
- Holds the current page.
- Exposes navigation commands.
- Enforces deterministic routing rules.
- Coordinates routing with pipeline execution state.

### State
```csharp
enum Page {
    Home,
    Editor,
    Execution,
    Settings
}

Page CurrentPage;
```

### Commands
- `NavigateHomeCommand`
- `NavigateEditorCommand`
- `NavigateExecutionCommand`
- `NavigateSettingsCommand`

### Rules
- Commands must not execute if they violate deterministic routing rules (see §7).
- Navigation must be synchronous and immediate.
- Navigation must not depend on UI thread timing or animations.

---

# 4. Navigation Entry Points

### 4.1 Application Startup
- Application starts on **Home Page**.
- If a file is opened immediately (e.g., via OS file association), navigate directly to **Editor Page**.

### 4.2 Sidebar Navigation
The sidebar provides deterministic navigation to:
- Home  
- Editor  
- Execution  
- Settings  

Sidebar navigation must respect execution constraints (see §7).

---

# 5. Execution‑Driven Navigation

Pipeline execution begins when the user clicks:

- **Run**
- **Explain**
- **Trace**

### 5.1 On Execution Start
When `EditorViewModel` begins execution:

1. `PipelineExecutionViewModel.IsRunning = true` (set on `pipeline_started`, per `ui-viewmodels.md` §6 — this is the single canonical execution-state flag; `EditorViewModel` has no separate `IsExecuting` property)
2. Execution buttons disabled
3. `NavigationViewModel.NavigateExecutionCommand` is invoked
4. Execution Page becomes active
5. Inspectors are cleared
6. UI waits for streaming events

### 5.2 Streaming Events
Navigation does not change during streaming.

Events update the Execution Page:

| Event Type | Navigation Behavior |
|------------|---------------------|
| `pipeline_started` | Stay on Execution Page |
| `raw_ast_generated` | Stay on Execution Page |
| `normalized_ast_generated` | Stay on Execution Page |
| `ir_generated` | Stay on Execution Page |
| `prompts_generated` | Stay on Execution Page |
| `model_outputs_generated` | Stay on Execution Page |
| `final_result_ready` | Stay on Execution Page |
| `pipeline_failed` | Stay on Execution Page |

Execution Page is the **only** page where streaming events may be rendered.

---

# 6. Error‑Driven Navigation

Errors may originate from:

- parser  
- normalizer  
- IR compiler  
- evaluator  
- model adapter  
- malformed request  
- malformed event  
- WebSocket disconnect  
- `pipeline_failed` events  

### Rules
- Errors must **not** trigger navigation away from Execution Page.
- Errors must be surfaced immediately:
  - global error banner  
  - inspector panels (if applicable)  
  - inline editor (validation errors)  
- Navigation must remain stable until the user explicitly navigates away.

---

# 7. Navigation Constraints (Strict Single Execution)

### 7.1 Forbidden Transitions During Execution
While `PipelineExecutionViewModel.IsRunning == true`, the following transitions are forbidden:

| From | To | Allowed? |
|------|----|----------|
| Execution | Home | ❌ Forbidden |
| Execution | Editor | ❌ Forbidden |
| Execution | Settings | ❌ Forbidden |

Only the following is allowed:

| From | To | Allowed? |
|------|----|----------|
| Execution | Execution | ✔ Allowed |

### 7.2 Allowed Transitions After Execution
When `final_result_ready` or `pipeline_failed` arrives:

- Execution buttons re‑enabled  
- All navigation commands become allowed again  
- User may leave Execution Page  
- If the user leaves Execution Page, the global error banner (if visible) clears as part of the transition — see `ui-error-handling.md` §8 and `ui-viewmodels.md` §10.

### 7.3 Rationale
This constraint ensures:

- deterministic state  
- no partial inspector rendering  
- no orphaned streaming events  
- no mismatched correlation IDs  
- no UI inconsistencies

---

# 8. Navigation Scenarios (BDD)

### 8.1 Successful Run Navigation
**GIVEN** the user is on Editor Page  
**WHEN** they click Run  
**THEN** navigation moves to Execution Page  
**AND** inspectors clear  
**AND** streaming events populate inspectors  
**AND** navigation remains locked until final_result_ready

### 8.2 Explain Navigation
**GIVEN** the user is on Editor Page  
**WHEN** they click Explain  
**THEN** navigation moves to Execution Page  
**AND** raw_ast_generated and normalized_ast_generated appear incrementally

### 8.3 Trace Navigation
**GIVEN** the user is on Editor Page  
**WHEN** they click Trace  
**THEN** navigation moves to Execution Page  
**AND** all inspector panels appear as events arrive

### 8.4 Pipeline Failure Navigation
**GIVEN** the user is on Execution Page  
**WHEN** pipeline_failed arrives  
**THEN** navigation remains on Execution Page  
**AND** error banner appears  
**AND** execution buttons re‑enable

### 8.5 Settings Navigation
**GIVEN** the user is on any page  
**WHEN** they click the gear icon  
**THEN** navigation moves to Settings Page  
**UNLESS** a pipeline is running  
**IN WHICH CASE** navigation is blocked

---

# 9. NavigationViewModel Responsibilities

### Must:
- enforce deterministic routing  
- block forbidden transitions  
- expose navigation commands  
- coordinate with execution state  
- ensure Execution Page is active during streaming  

### Must Not:
- depend on Views  
- depend on inspector state  
- depend on HTTP or WebSocket services  
- perform pipeline logic  
- perform error handling logic  

---

# 10. Non‑Goals

Routing does **not** support:

- parallel pipeline executions  
- queued executions  
- cancellation  
- nondeterministic animations  
- implicit transitions  
- plugin pages  
- multi‑file project navigation  
- custom routing logic in Views  

---

# 11. Future Extensions

Potential enhancements:

- queued execution mode  
- cancelable pipelines  
- animated transitions (if deterministic)  
- multi‑file project navigation  
- plugin pages  

---

# Summary

Limelight‑X routing is deterministic, MVVM‑driven, and tightly integrated with the streaming pipeline model.  
Execution always routes to the Execution Page, which remains active until the pipeline completes or fails.  
Navigation constraints ensure predictable behavior, stable inspector rendering, and strict single‑execution semantics.