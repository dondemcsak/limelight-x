# BDD — UI Error Cases (Streaming Edition)

## Purpose
This document defines all deterministic error‑handling scenarios for the Limelight‑X UI under the **event‑streaming API**.  
It specifies how the UI must behave when encountering pipeline errors, API errors, transport errors, validation errors, and inspector rendering errors.

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
- tab switching is never blocked by execution or error state  

---

# 2. Pipeline Error Scenarios

## 2.1 Parser Error (Streaming)
**GIVEN** the user clicks Run  
**WHEN** the backend emits `pipeline_failed` with `ERR_CNL_PARSE`  
**THEN** the global error banner appears  
**SO THAT** the user sees the parser failure immediately  
**AS MEASURED BY** `ErrorBannerViewModel.IsVisible == true`

## 2.2 Normalizer Error
**GIVEN** execution is running  
**WHEN** `pipeline_failed` arrives with `ERR_CNL_NORMALIZE`  
**THEN** inspector panels remain visible but incomplete  
**SO THAT** the user sees where normalization failed  
**AS MEASURED BY** `HasErrors == true`

## 2.3 IR Compiler Error
**GIVEN** normalized AST is visible  
**WHEN** `pipeline_failed` arrives with `ERR_IR_COMPILE`  
**THEN** IR panel shows an inline error  
**SO THAT** the user sees IR compilation failure  
**AS MEASURED BY** `IrViewModel.HasErrors == true`

## 2.4 Evaluator Fatal Error
**GIVEN** execution is running  
**WHEN** `pipeline_failed` arrives with `ERR_EVALUATOR_FATAL`  
**THEN** fatal styling appears  
**SO THAT** the user understands the pipeline cannot continue  
**AS MEASURED BY** `ErrorBannerViewModel.Severity == "fatal"`

## 2.5 Model Adapter Fatal Error
**GIVEN** prompts have been generated  
**WHEN** `pipeline_failed` arrives with `ERR_MODEL_ADAPTER`  
**THEN** ModelOutputPanel shows fatal error  
**SO THAT** the user sees model failure context  
**AS MEASURED BY** `ModelOutputViewModel.HasErrors == true`

---

# 3. API Error Scenarios

## 3.1 Malformed Request Body
**GIVEN** the user clicks Run  
**WHEN** the backend rejects the request with `ERR_MALFORMED_REQUEST`  
**THEN** no tab or workspace-area change occurs  
**SO THAT** the user stays on the same tab, editing  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab` unchanged, error banner appears in that tab

## 3.2 Missing `source` Field
**GIVEN** the user triggers execution with empty text  
**WHEN** backend returns `ERR_MISSING_FIELD`  
**THEN** inline editor error appears  
**SO THAT** the user sees missing input  
**AS MEASURED BY** `SyntaxErrors.Count > 0`

## 3.3 Backend Startup Failure
**GIVEN** the user opens the Settings modal  
**WHEN** they save invalid backend configuration  
**THEN** backend fails to start  
**SO THAT** error banner appears inside the Settings modal  
**AS MEASURED BY** `ErrorBannerViewModel.IsVisible == true`

---

# 4. Transport Error Scenarios

## 4.1 WebSocket Disconnect During Execution
**GIVEN** execution is running  
**WHEN** WebSocket disconnects  
**THEN** global error banner appears  
**SO THAT** the user sees transport failure  
**AS MEASURED BY** `PipelineExecutionViewModel.HasErrors == true`

## 4.2 Malformed Event Payload
**GIVEN** execution is running  
**WHEN** backend sends invalid JSON  
**THEN** streaming stops  
**SO THAT** the UI remains stable  
**AS MEASURED BY** `IsRunning == false`

## 4.3 Event With Wrong Correlation ID
**GIVEN** active correlation ID = `abc-123`  
**WHEN** event arrives with `xyz-999`  
**THEN** UI ignores the event  
**SO THAT** no cross‑execution contamination occurs  
**AS MEASURED BY** unchanged inspector state

---

# 5. Inspector Error Scenarios

## 5.1 Raw AST Rendering Error
**GIVEN** `raw_ast_generated` arrives  
**WHEN** RawAstPanel fails to render  
**THEN** InspectorErrorPanel appears  
**SO THAT** the user sees rendering failure  
**AS MEASURED BY** `RawAstViewModel.HasErrors == true`

## 5.2 Normalized AST Rendering Error
**GIVEN** normalized AST arrives  
**WHEN** rendering fails  
**THEN** NormalizedAstPanel shows error  
**SO THAT** user sees failure context  
**AS MEASURED BY** `NormalizedAstViewModel.HasErrors == true`

## 5.3 IR Rendering Error
**GIVEN** IR arrives  
**WHEN** rendering fails  
**THEN** IR panel shows error  
**SO THAT** user sees IR failure  
**AS MEASURED BY** `IrViewModel.HasErrors == true`

## 5.4 Prompt Rendering Error
**GIVEN** prompts arrive  
**WHEN** rendering fails  
**THEN** PromptPanel shows error  
**SO THAT** user sees prompt failure  
**AS MEASURED BY** `PromptViewModel.HasErrors == true`

## 5.5 Model Output Rendering Error
**GIVEN** model outputs arrive  
**WHEN** rendering fails  
**THEN** ModelOutputPanel shows error  
**SO THAT** user sees output failure  
**AS MEASURED BY** `ModelOutputViewModel.HasErrors == true`

---

# 6. Editor Error Scenarios

## 6.1 Inline Parser Error
**GIVEN** user edits CNL  
**WHEN** `/explain` returns parser error  
**THEN** inline error appears  
**SO THAT** user sees grammar issue  
**AS MEASURED BY** red underline + margin marker

## 6.2 Inline Grammar Error
**GIVEN** user edits CNL  
**WHEN** `/explain` returns grammar error  
**THEN** inline error appears  
**SO THAT** user sees grammar issue  
**AS MEASURED BY** updated `SyntaxErrors`

## 6.3 Inline Expression Hole Error
**GIVEN** user edits CNL  
**WHEN** `/explain` returns hole error  
**THEN** inline error appears  
**SO THAT** user sees missing expression  
**AS MEASURED BY** error marker at hole location

---

# 7. Execution Concurrency Error Scenarios

General execution-lock mechanics (when Run/Explain/Settings lock and unlock, and what remains free) are authoritative in `bdd-ui-navigation.md` §7. The scenarios below are the error-triggered special case of those same rules and should stay consistent with it — if the two ever appear to disagree, `bdd-ui-navigation.md` §7 wins.

## 7.1 New Execution Blocked While Another Is In Flight
**GIVEN** execution is running in some tab  
**WHEN** the user clicks Run or Explain in a different tab  
**THEN** the click has no effect (buttons disabled)  
**SO THAT** UI remains consistent  
**AS MEASURED BY** that other tab's `EditorViewModel.CanExecute == false`

## 7.2 Execution Allowed Again After Error
**GIVEN** a pipeline failed in some tab  
**WHEN** the user clicks Run or Explain in any tab  
**THEN** execution succeeds  
**SO THAT** the user can retry or continue elsewhere  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning` becomes `true` for the new execution

---

# 8. Fatal Error Scenarios

## 8.1 Fatal Evaluator Error
**GIVEN** evaluator encounters fatal error  
**WHEN** `pipeline_failed` arrives with severity `fatal`  
**THEN** fatal styling appears  
**SO THAT** user understands pipeline cannot continue  
**AS MEASURED BY** red banner with fatal indicator

## 8.2 Fatal Model Adapter Error
**GIVEN** model adapter fails  
**WHEN** fatal error arrives  
**THEN** ModelOutputPanel shows fatal error  
**SO THAT** user sees model failure  
**AS MEASURED BY** `ModelOutputViewModel.Severity == "fatal"`

---

# 9. Error Clearing Scenarios

## 9.1 Clear on New Execution
**GIVEN** previous execution failed  
**WHEN** new `pipeline_started` arrives  
**THEN** all errors clear  
**SO THAT** UI begins fresh execution  
**AS MEASURED BY** empty error banner + cleared inspectors

## 9.2 Clear on Dismiss
**GIVEN** error banner is visible  
**WHEN** user clicks Dismiss  
**THEN** banner hides  
**SO THAT** user can continue workflow  
**AS MEASURED BY** `ErrorBannerViewModel.IsVisible == false`

## 9.3 Switching Tabs Does Not Clear Another Tab's Banner
**GIVEN** a tab's error banner is visible  
**WHEN** the user switches to a different tab and back  
**THEN** the banner is still visible on the original tab, unchanged  
**AND** no other tab shows this banner while it was the active tab  
**SO THAT** each tab's error state stays independent and predictable  
**AS MEASURED BY** the owning tab's `ErrorBannerViewModel.IsVisible` unchanged by the tab switch; no other tab's `ErrorBannerViewModel.IsVisible` becomes `true` as a side effect

This reverses the previous single‑page‑model rule ("navigating away from Execution Page clears the banner") — see `ui-error-handling.md` §8 for the full per‑tab clearing rules.

---

# 10. Non‑Goals

These scenarios do **not** cover:

- parallel executions (per‑tab concurrent execution is a possible future extension)  
- queued executions  
- cancellation  
- plugin inspectors  
- nondeterministic animations  

---

# Summary

These BDD error‑case scenarios define all deterministic error behaviors in the Limelight‑X UI under the streaming API.  
They ensure predictable error rendering, stable inspector behavior, strict navigation constraints, and robust handling of pipeline, API, transport, and UI errors.