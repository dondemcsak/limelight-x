# UI Testing (Streaming Edition)

## Purpose
This document defines the complete testing strategy for the Limelight‑X UI.  
It specifies unit tests, integration tests, streaming tests, inspector tests, tab/workspace tests, and error‑handling tests under the **event‑streaming API**.

This specification is authoritative.  
All implementation must follow this testing model exactly.

The UI is deterministic, MVVM‑pure, and driven entirely by ViewModels and streaming events.  
All tests must validate deterministic behavior, app‑wide single‑execution mode, and correct incremental updates.

---

# 1. Architectural Testing Principles

1. **Deterministic Tests**  
   - No nondeterministic timing.  
   - No race conditions.  
   - No parallel pipeline executions.

2. **MVVM‑Pure Tests**  
   - Views are not tested directly.  
   - ViewModels are tested in isolation.  
   - Services are mocked.

3. **Streaming‑Aware Tests**  
   - Tests simulate WebSocket event streams.  
   - Tests validate incremental inspector updates.  
   - Tests validate correlation‑ID filtering.

4. **App‑Wide Single Execution**  
   - Tests ensure execution commands disable correctly, app‑wide, across all tabs.  
   - Tests ensure tab switching/opening/closing is never blocked by execution state.  
   - Tests ensure state resets on `pipeline_started`, scoped to the executing tab.

---

# 2. Test Categories

The UI requires the following test suites:

1. **Unit Tests**
2. **Integration Tests**
3. **Streaming Tests**
4. **Inspector Tests**
5. **Tab & Workspace Tests**
6. **Layout & Resize Tests**
7. **Error‑Handling Tests**
8. **Settings Tests**
9. **Execution Workflow Tests**
10. **Logging Tests**

Each suite is described below.

---

# 3. Unit Tests

Unit tests validate individual ViewModels and services.

### 3.1 EditorViewModel Tests
- Run/Explain commands disable app‑wide while any tab has an execution in flight.  
- Live validation updates syntax errors deterministically, and is unaffected by the app‑wide execution lock.  
- Invalid CNL triggers inline errors.  
- `CanExecute` reflects `IExecutionLockService.IsAnyExecutionRunning` correctly.

### 3.2 PipelineExecutionViewModel Tests
- State clears on `pipeline_started`, scoped to that instance's tab.  
- Events update correct inspector ViewModels for that tab only.  
- Mismatched correlation IDs are ignored.  
- `IsRunning` toggles correctly per tab.  
- `HasErrors` toggles on `pipeline_failed`.  
- A second tab's `PipelineExecutionViewModel` is unaffected by another tab's event stream.

### 3.3 Inspector ViewModel Tests
Each inspector must:
- clear state on `pipeline_started` (for its own tab)  
- update deterministically on its event  
- remain stable when collapsed  
- never reorder or buffer data  

### 3.4 SettingsViewModel Tests
- Invalid settings block Save.  
- Valid settings restart backend.  
- Errors surface deterministically.

### 3.5 IExecutionLockService Tests
- `TryAcquire` succeeds only when no tab currently holds the lock.  
- `TryAcquire` fails while another tab holds the lock.  
- `Release` allows a subsequent `TryAcquire` to succeed.  
- `ExecutionLockChanged` fires exactly when `IsAnyExecutionRunning` changes.

### 3.6 WorkspaceViewModel / FileTreeViewModel Tests
- Opening a folder populates the tree from the filesystem.  
- Opening a `.llx` file creates/focuses a `CnlTabViewModel`; opening any other file creates/focuses a `PlainTextTabViewModel`.  
- Opening a file already open in a tab focuses the existing tab rather than creating a duplicate.  
- Closing a dirty tab triggers the unsaved‑changes confirmation dialog; closing a clean tab does not.

---

# 4. Integration Tests

Integration tests validate multi‑ViewModel workflows.

### 4.1 Editor → Execution Workflow (Run)
- Clicking Run invokes `/trace` and renders in place, inside the same tab.  
- Inspectors clear immediately in that tab.  
- Streaming events populate all six inspectors incrementally, in that tab.  
- Execution buttons re‑enable app‑wide only after `final_result_ready`.

### 4.2 Explain Workflow
- Raw AST and Normalized AST appear in correct order, in that tab.  
- No final result, prompts, or model outputs appear (`/explain` never invokes the evaluator; the sequence ends at `normalized_ast_generated`).

### 4.3 Cross‑Tab Execution Lock Workflow
- Starting Run in tab A disables Run/Explain in tab B (and every other open tab) and disables the Settings gear.  
- Switching the active tab from A to B and back does not affect tab A's in‑flight execution or its lock ownership.  
- Closing tab B (not the executing tab) while tab A's execution is in flight has no effect on the lock.  
- Once tab A's execution reaches `final_result_ready`/`pipeline_failed`, Run/Explain re‑enable in every open tab.

---

# 5. Streaming Tests

Streaming tests simulate WebSocket event sequences.

### 5.1 Event Ordering Tests
Given a sequence:
```
pipeline_started
raw_ast_generated
normalized_ast_generated
ir_generated
( prompt_generated
  model_output_generated ) × N
final_result_ready
```
where N is the number of model-calling IR operations in the program (0 or more); each pair may repeat, unlike the other listed events which fire exactly once per execution.

The UI must:
- update the owning tab's inspectors in exact order  
- never reorder events  
- never buffer events  
- never drop events  

### 5.2 Correlation ID Tests
Given:
- active correlation ID = `abc-123`
- event with correlation ID = `xyz-999`

UI must:
- ignore the event  
- not update inspectors  
- not change execution state  

### 5.3 Partial Stream Tests
Simulate missing events:
- UI must remain stable  
- UI must not crash  
- UI must not auto‑complete pipeline  
- UI must surface transport errors in the owning tab

### 5.4 WebSocket Disconnect Tests
Simulate disconnect mid‑pipeline:
- UI must show the owning tab's error banner  
- UI must stop updating that tab's inspectors  
- UI must re‑enable execution buttons app‑wide  

---

# 6. Inspector Tests

All six inspector panels are always rendered from tab-open, starting `IsCollapsed == true`; "auto-expands" below means `IsCollapsed` transitions to `false`, not that the panel is inserted into the layout.

Each inspector must be tested for:

### 6.1 Incremental Updates
- Raw AST panel auto-expands only after `raw_ast_generated`.  
- Normalized AST panel auto-expands only after `normalized_ast_generated`.  
- IR panel auto-expands only after `ir_generated`.  
- Prompt panel auto-expands after the first `prompt_generated`; the collection's count increments with each subsequent `prompt_generated` rather than being fixed after the first, and the panel does not re-collapse or re-expand on subsequent events.  
- Model Output panel auto-expands after the first `model_output_generated`; the collection's count increments with each subsequent `model_output_generated` rather than being fixed after the first, and the panel does not re-collapse or re-expand on subsequent events.  
- Final Result panel auto-expands only after `final_result_ready`.

### 6.2 Collapse/Expand Behavior
- Collapsed state must not affect updates.  
- Expanded state must show updated content immediately.  
- All six panels reset to `IsCollapsed == true` on `pipeline_started`, regardless of how the previous run left them.  
- A panel not reached by the current execution (e.g. all six on a failed Explain before `normalized_ast_generated`, or Prompts/Model Outputs/IR on an Explain execution) remains collapsed and present in the layout.

### 6.3 Error Rendering
- Inspector must show inline error panel if its event fails.  
- Inspector must remain visible.

### 6.4 Resize Behavior
- Dragging a panel's `SplitterControl` handle changes that panel's `Height` and trades height only with its immediate next-neighbor panel below (classic two-panel splitter behavior); panels further down keep their own `Height` unchanged and simply reposition.  
- Resizing a panel does not change its `IsCollapsed` state or its content.  
- A panel's content area shows a scrollbar only when its content exceeds its current `Height`; no scrollbar when content fits.

### 6.5 Auto-Scroll Behavior
- Each `prompt_generated` event scrolls the Prompt panel to reveal the newly appended entry, unconditionally (including the entry that triggers the first auto-expand).  
- Each `model_output_generated` event scrolls the Model Output panel to reveal the newly appended entry, unconditionally.  
- Auto-scroll behavior does not depend on, or track, the panel's prior scroll position.

---

# 7. Tab & Workspace Tests

### 7.1 Tab Lifecycle Tests
- Opening a file from the tree creates a tab of the correct type (`CnlTabViewModel` vs `PlainTextTabViewModel`).  
- Opening an already‑open file focuses the existing tab.  
- Closing a tab removes it from `WorkspaceViewModel.OpenTabs` and disposes its owned ViewModels.

### 7.2 Execution Lock Tests
While `IExecutionLockService.IsAnyExecutionRunning == true`:
- Starting Run/Explain in the executing tab is blocked (already running)  
- Starting Run/Explain in any other tab is blocked  
- Opening the Settings modal is blocked  
- Switching tabs is allowed  
- Opening a new tab is allowed  
- Closing any tab is allowed

### 7.3 Post‑Execution Tests
After `final_result_ready` or `pipeline_failed`:
- Run/Explain re‑enable on every open tab  
- The Settings gear re‑enables  

### 7.4 Error Persistence Across Tab Switch Tests
- Errors must:
  - remain visible on their owning tab after switching away and back  
  - not appear on, or affect, any other tab  
  - not reset pipeline state

---

# 8. Layout & Resize Tests

### 8.1 Editor/Panel Splitter Tests
- Dragging the `SplitterControl` between the editor and the execution panel updates `CnlTabViewModel.EditorPaneRatio` and both regions' rendered heights to match.  
- The editor shows a vertical scrollbar exactly when its rendered content height exceeds its current allocated height, and no scrollbar otherwise.

### 8.2 Panel Accordion Resize Tests
- Dragging one inspector panel's `SplitterControl` handle changes that panel's `Height` and trades height only with its immediate next-neighbor panel below (classic two-panel splitter behavior); panels further down keep their own `Height` unchanged and simply reposition.  
- Resizing a panel does not change its `IsCollapsed` state or its content.  
- A panel's content area shows a scrollbar exactly when its content exceeds its current `Height`.

### 8.3 Auto-Scroll Tests
- Each `prompt_generated` event scrolls the Prompt panel to reveal the newly appended entry, unconditionally.  
- Each `model_output_generated` event scrolls the Model Output panel to reveal the newly appended entry, unconditionally.  
- Auto-scroll behavior is independent of the panel's prior scroll position (no "stick to bottom unless scrolled up" tracking).

---

# 9. Error‑Handling Tests

### 9.1 Pipeline Error Tests
Simulate `pipeline_failed`:
- The owning tab's error banner appears  
- That tab's inspectors remain visible  
- Execution buttons re‑enable app‑wide  
- No tab switch is forced

### 9.2 Fatal Error Tests
Simulate evaluator/model adapter fatal error:
- Fatal styling appears on the owning tab  
- Streaming stops for that tab  
- Execution buttons re‑enable app‑wide  

### 9.3 Inline Editor Error Tests
Simulate `/explain` validation errors:
- Editor highlights error  
- Margin markers appear  
- Error list updates deterministically  

### 9.4 Transport Error Tests
Simulate:
- malformed event  
- invalid JSON  
- WebSocket disconnect  

UI must:
- surface error immediately in the owning tab  
- stop that tab's execution  
- re‑enable buttons app‑wide  

---

# 10. Settings Tests

### 10.1 Validation Tests
- Invalid port blocks Save  
- Missing API key blocks Save  
- Invalid log path blocks Save  

### 10.2 Backend Restart Tests
- Save triggers backend restart  
- Errors surface deterministically inside the Settings modal  
- UI remains stable during restart  

### 10.3 Gate Tests
- The Settings gear icon is disabled while `IExecutionLockService.IsAnyExecutionRunning == true`  
- The gear re‑enables once the in‑flight execution reaches a terminal state

---

# 11. Execution Workflow Tests

### 11.1 Run Workflow
- Run invokes `/trace`.  
- All six inspector panels (Raw AST, Normalized AST, IR, Prompts, Model Outputs, Final Result) are present from tab-open and auto-expand in order.  
- Final Result panel auto-expands last.

### 11.2 Explain Workflow
- Raw AST panel auto-expands  
- Normalized AST panel auto-expands  
- IR, Prompts, and Model Output panels remain collapsed (`/explain` never invokes the evaluator)  

---

# 12. Logging Tests

### 12.1 Default Location
- No custom `LogPath` configured  
- An error is added to any of the four logged collections (`WorkspaceViewModel.Errors`, a tab's `EditorViewModel.ValidationErrors`, a tab's `PipelineExecutionViewModel.Errors`, `SettingsViewModel.Errors`)  
- Entry is appended to `%APPDATA%\LimelightX\Limelight-x-log.txt`

### 12.2 Custom LogPath
- `LogPath` set to a custom absolute directory  
- An error is added to any of the four logged collections  
- Entry is appended to `<LogPath>\Limelight-x-log.txt` instead of the default location

### 12.3 Append Across Sessions
- Log file already contains entries from a prior session  
- App restarts and a new error occurs  
- Both prior and new entries are present in the file (never truncated)

### 12.4 Line Format And Severity Mapping
- An error with a given `Severity`/`Code`/`Message`/`Location` is logged  
- The written line matches `[<UTC ISO-8601 timestamp>] [<LogLevel>] <Code>: <Message>` (plus location suffix when present)  
- `Severity` maps to `LogLevel` exactly as: `Info`→`Information`, `Warning`→`Warning`, `Error`→`Error`, `Fatal`→`Critical`

### 12.5 Write Failure Is Non‑Fatal
- Log directory is unwritable (e.g. permissions)  
- An error occurs  
- The app does not crash, no new user-facing error is raised for the write failure itself, and the original error still appears through its normal UI surface (banner/inline/inspector)

---

# 13. Non‑Goals

UI testing does **not** include:

- View testing  
- nondeterministic animations  
- parallel pipeline executions  
- queued executions  
- cancellation  
- plugin inspectors  

---

# 14. Future Extensions

Potential enhancements:

- automated inspector diffing  
- visual IR graph testing  
- per‑tab concurrent execution testing (if per‑tab independent execution is implemented, see `ui-viewmodels.md` §15)  
- performance tests for large pipelines  
- timing‑based observability tests  

---

# Summary

Limelight‑X UI testing validates deterministic MVVM behavior, app‑wide single‑execution mode, and real‑time streaming updates.  
All tests simulate WebSocket event streams, verify incremental inspector updates scoped to the correct tab, verify deterministic editor/panel and accordion resize behavior with unconditional auto-scroll for Prompts/Model Outputs, enforce the cross‑tab execution lock and free tab/workspace navigation, and ensure robust error handling across the entire workflow.
