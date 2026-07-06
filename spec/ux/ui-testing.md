# UI Testing (Streaming Edition)

## Purpose
This document defines the complete testing strategy for the Limelight‑X UI.  
It specifies unit tests, integration tests, streaming tests, inspector tests, navigation tests, and error‑handling tests under the **event‑streaming API**.

This specification is authoritative.  
All implementation must follow this testing model exactly.

The UI is deterministic, MVVM‑pure, and driven entirely by ViewModels and streaming events.  
All tests must validate deterministic behavior, strict single‑execution mode, and correct incremental updates.

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

4. **Strict Single Execution**  
   - Tests ensure execution commands disable correctly.  
   - Tests ensure navigation locks during execution.  
   - Tests ensure state resets on `pipeline_started`.

---

# 2. Test Categories

The UI requires the following test suites:

1. **Unit Tests**
2. **Integration Tests**
3. **Streaming Tests**
4. **Inspector Tests**
5. **Navigation Tests**
6. **Error‑Handling Tests**
7. **Settings Tests**
8. **Execution Workflow Tests**
9. **Logging Tests**

Each suite is described below.

---

# 3. Unit Tests

Unit tests validate individual ViewModels and services.

### 3.1 EditorViewModel Tests
- Run/Explain/Trace commands disable during execution.  
- Live validation updates syntax errors deterministically.  
- Invalid CNL triggers inline errors.  
- `CanExecute` reflects correct state.

### 3.2 PipelineExecutionViewModel Tests
- State clears on `pipeline_started`.  
- Events update correct inspector ViewModels.  
- Mismatched correlation IDs are ignored.  
- `IsRunning` toggles correctly.  
- `HasErrors` toggles on `pipeline_failed`.

### 3.3 Inspector ViewModel Tests
Each inspector must:
- clear state on `pipeline_started`  
- update deterministically on its event  
- remain stable when collapsed  
- never reorder or buffer data  

### 3.4 SettingsViewModel Tests
- Invalid settings block Save.  
- Valid settings restart backend.  
- Errors surface deterministically.

---

# 4. Integration Tests

Integration tests validate multi‑ViewModel workflows.

### 4.1 Editor → Execution Workflow
- Clicking Run navigates to Execution Page.  
- Inspectors clear immediately.  
- Streaming events populate inspectors incrementally.  
- Execution buttons re‑enable only after final_result_ready.

### 4.2 Explain Workflow
- Raw AST and Normalized AST appear in correct order.  
- No final result, prompts, or model outputs appear (`/explain` never invokes the evaluator; the sequence ends at `normalized_ast_generated`).

### 4.3 Trace Workflow
- All inspector panels appear in correct order.  
- IR appears after normalized AST.  
- Prompts appear before model outputs.  
- Final result appears last.

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
prompts_generated
model_outputs_generated
final_result_ready
```
The UI must:
- update inspectors in exact order  
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
- UI must surface transport errors

### 5.4 WebSocket Disconnect Tests
Simulate disconnect mid‑pipeline:
- UI must show global error banner  
- UI must stop updating inspectors  
- UI must re‑enable execution buttons  

---

# 6. Inspector Tests

Each inspector must be tested for:

### 6.1 Incremental Updates
- Raw AST appears only after `raw_ast_generated`.  
- Normalized AST appears only after `normalized_ast_generated`.  
- IR appears only after `ir_generated`.  
- Prompts appear only after `prompts_generated`.  
- Model outputs appear only after `model_outputs_generated`.  
- Final result appears only after `final_result_ready`.

### 6.2 Collapse/Expand Behavior
- Collapsed state must not affect updates.  
- Expanded state must show updated content immediately.

### 6.3 Error Rendering
- Inspector must show inline error panel if its event fails.  
- Inspector must remain visible.

---

# 7. Navigation Tests

### 7.1 Execution Lock Tests
While `IsRunning == true`:
- Navigation to Home is blocked  
- Navigation to Editor is blocked  
- Navigation to Settings is blocked  
- Navigation to Execution is allowed  

### 7.2 Post‑Execution Navigation
After `final_result_ready` or `pipeline_failed`:
- All navigation commands re‑enable  
- User may leave Execution Page  

### 7.3 Error Navigation Tests
Errors must:
- not trigger navigation  
- not hide inspector state  
- not reset pipeline state  

---

# 8. Error‑Handling Tests

### 8.1 Pipeline Error Tests
Simulate `pipeline_failed`:
- Global error banner appears  
- Inspectors remain visible  
- Execution buttons re‑enable  
- Navigation remains on Execution Page  

### 8.2 Fatal Error Tests
Simulate evaluator/model adapter fatal error:
- Fatal styling appears  
- Streaming stops  
- Execution buttons re‑enable  

### 8.3 Inline Editor Error Tests
Simulate `/explain` validation errors:
- Editor highlights error  
- Margin markers appear  
- Error list updates deterministically  

### 8.4 Transport Error Tests
Simulate:
- malformed event  
- invalid JSON  
- WebSocket disconnect  

UI must:
- surface error immediately  
- stop execution  
- re‑enable buttons  

---

# 9. Settings Tests

### 9.1 Validation Tests
- Invalid port blocks Save  
- Missing API key blocks Save  
- Invalid log path blocks Save  

### 9.2 Backend Restart Tests
- Save triggers backend restart  
- Errors surface deterministically  
- UI remains stable during restart  

---

# 10. Execution Workflow Tests

### 10.1 Run Workflow
- Only final_result_ready appears  
- No AST/IR/prompts/model outputs appear  

### 10.2 Explain Workflow
- Raw AST appears  
- Normalized AST appears  
- No final result, prompts, or model outputs appear  

### 10.3 Trace Workflow
- All inspectors appear in correct order  
- Final result appears last  

---

# 11. Logging Tests

### 11.1 Default Location
- No custom `LogPath` configured  
- An error is added to any of the four logged collections  
- Entry is appended to `%APPDATA%\LimelightX\Limelight-x-log.txt`

### 11.2 Custom LogPath
- `LogPath` set to a custom absolute directory  
- An error is added to any of the four logged collections  
- Entry is appended to `<LogPath>\Limelight-x-log.txt` instead of the default location

### 11.3 Append Across Sessions
- Log file already contains entries from a prior session  
- App restarts and a new error occurs  
- Both prior and new entries are present in the file (never truncated)

### 11.4 Line Format And Severity Mapping
- An error with a given `Severity`/`Code`/`Message`/`Location` is logged  
- The written line matches `[<UTC ISO-8601 timestamp>] [<LogLevel>] <Code>: <Message>` (plus location suffix when present)  
- `Severity` maps to `LogLevel` exactly as: `Info`→`Information`, `Warning`→`Warning`, `Error`→`Error`, `Fatal`→`Critical`

### 11.5 Write Failure Is Non‑Fatal
- Log directory is unwritable (e.g. permissions)  
- An error occurs  
- The app does not crash, no new user-facing error is raised for the write failure itself, and the original error still appears through its normal UI surface (banner/inline/inspector)

---

# 12. Non‑Goals

UI testing does **not** include:

- View testing  
- nondeterministic animations  
- parallel pipeline executions  
- queued executions  
- cancellation  
- plugin inspectors  
- multi‑file project workflows  

---

# 13. Future Extensions

Potential enhancements:

- automated inspector diffing  
- visual IR graph testing  
- multi‑file project testing  
- performance tests for large pipelines  
- timing‑based observability tests  

---

# Summary

Limelight‑X UI testing validates deterministic MVVM behavior, strict single‑execution mode, and real‑time streaming updates.  
All tests simulate WebSocket event streams, verify incremental inspector updates, enforce navigation constraints, and ensure robust error handling across the entire workflow.