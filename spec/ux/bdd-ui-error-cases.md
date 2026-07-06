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

- strict single‑execution mode  
- deterministic MVVM state  
- incremental WebSocket event streaming  
- correlation‑ID filtering  
- no parallel executions  

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
**THEN** no navigation occurs  
**SO THAT** the user stays on Editor Page  
**AS MEASURED BY** `CurrentPage == Editor`

## 3.2 Missing `source` Field
**GIVEN** the user triggers execution with empty text  
**WHEN** backend returns `ERR_MISSING_FIELD`  
**THEN** inline editor error appears  
**SO THAT** the user sees missing input  
**AS MEASURED BY** `SyntaxErrors.Count > 0`

## 3.3 Backend Startup Failure
**GIVEN** the user opens Settings Page  
**WHEN** they save invalid backend configuration  
**THEN** backend fails to start  
**SO THAT** error banner appears  
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

# 7. Navigation Error Scenarios

General navigation-guard mechanics (when navigation/buttons lock and unlock) are authoritative in `bdd-ui-navigation.md` §7. The scenarios below are the error-triggered special case of those same rules and should stay consistent with it — if the two ever appear to disagree, `bdd-ui-navigation.md` §7 wins.

## 7.1 Navigation Blocked During Execution
**GIVEN** execution is running  
**WHEN** user clicks Home  
**THEN** navigation is blocked  
**SO THAT** UI remains consistent  
**AS MEASURED BY** `CurrentPage == Execution`

## 7.2 Navigation Allowed After Error
**GIVEN** pipeline failed  
**WHEN** user clicks Editor  
**THEN** navigation succeeds  
**SO THAT** user can continue editing  
**AS MEASURED BY** `CurrentPage == Editor`

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

## 9.3 Clear on Navigation
**GIVEN** error banner is visible  
**WHEN** user navigates away from Execution Page  
**THEN** banner clears  
**SO THAT** UI remains clean  
**AS MEASURED BY** banner hidden on new page

---

# 10. Non‑Goals

These scenarios do **not** cover:

- parallel executions  
- queued executions  
- cancellation  
- plugin inspectors  
- multi‑file workflows  
- nondeterministic animations  

---

# Summary

These BDD error‑case scenarios define all deterministic error behaviors in the Limelight‑X UI under the streaming API.  
They ensure predictable error rendering, stable inspector behavior, strict navigation constraints, and robust handling of pipeline, API, transport, and UI errors.