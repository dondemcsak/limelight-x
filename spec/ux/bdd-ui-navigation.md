# BDD — UI Navigation (Streaming Edition)

## Purpose
This document defines all deterministic navigation scenarios for the Limelight‑X UI under the **event‑streaming API**.  
It specifies how the UI must transition between pages during editing, execution, streaming, error handling, and settings updates.

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

# 2. Page Definitions

The UI contains four pages:

1. **Home Page**  
2. **Editor Page**  
3. **Execution Page**  
4. **Settings Page**

Navigation is controlled exclusively by `NavigationViewModel`.

---

# 3. Startup Navigation Scenarios

## 3.1 Application Startup
**GIVEN** the application launches  
**WHEN** no file is provided  
**THEN** UI navigates to Home Page  
**SO THAT** the user sees entry‑point actions  
**AS MEASURED BY** `CurrentPage == Home`

## 3.2 Startup With File
**GIVEN** the application launches with a `.llx` file  
**WHEN** the file loads successfully  
**THEN** UI navigates to Editor Page  
**SO THAT** the user can begin editing immediately  
**AS MEASURED BY** `CurrentPage == Editor`

---

# 4. Editor Navigation Scenarios

## 4.1 Navigate to Editor
**GIVEN** the user is on Home Page  
**WHEN** they click “Open File” or “Editor”  
**THEN** UI navigates to Editor Page  
**SO THAT** the user can edit CNL  
**AS MEASURED BY** `CurrentPage == Editor`

## 4.2 Editor → Settings
**GIVEN** the user is on Editor Page  
**WHEN** they click the gear icon  
**THEN** UI navigates to Settings Page  
**SO THAT** backend configuration can be updated  
**AS MEASURED BY** `CurrentPage == Settings`

---

# 5. Execution Navigation Scenarios (Streaming)

## 5.1 Editor → Execution on Run
**GIVEN** the user is on Editor Page  
**WHEN** they click Run  
**THEN** UI navigates to Execution Page  
**SO THAT** the user sees pipeline progress  
**AS MEASURED BY** `CurrentPage == Execution`

## 5.2 Editor → Execution on Explain
**GIVEN** the user is on Editor Page  
**WHEN** they click Explain  
**THEN** UI navigates to Execution Page  
**SO THAT** the user sees AST and normalized AST  
**AS MEASURED BY** `CurrentPage == Execution`

## 5.3 Editor → Execution on Trace
**GIVEN** the user is on Editor Page  
**WHEN** they click Trace  
**THEN** UI navigates to Execution Page  
**SO THAT** the user sees full pipeline details  
**AS MEASURED BY** `CurrentPage == Execution`

---

# 6. Streaming Event Navigation Scenarios

## 6.1 Stay on Execution During Streaming
**GIVEN** execution is running  
**WHEN** any streaming event arrives  
**THEN** UI remains on Execution Page  
**SO THAT** pipeline progress is visible  
**AS MEASURED BY** `CurrentPage == Execution`

Events include:
- `pipeline_started`  
- `raw_ast_generated`  
- `normalized_ast_generated`  
- `ir_generated`  
- `prompts_generated`  
- `model_outputs_generated`  
- `final_result_ready`  

## 6.2 Final Result Does Not Trigger Navigation
**GIVEN** `final_result_ready` arrives  
**WHEN** execution completes  
**THEN** UI stays on Execution Page  
**SO THAT** user can inspect results  
**AS MEASURED BY** `CurrentPage == Execution`

---

# 7. Navigation Lock Scenarios (Strict Single Execution)

This section is the authoritative source for navigation-guard mechanics (when navigation/buttons lock and unlock). `bdd-ui-error-cases.md` §7 covers only the error-triggered variant of these same transitions (navigation behavior specifically after a `pipeline_failed`) and should be read as a special case of the rules here, not a duplicate — if the two ever appear to disagree, this section wins.

## 7.1 Block Navigation During Execution
**GIVEN** execution is running  
**WHEN** the user attempts to navigate to Home, Editor, or Settings  
**THEN** navigation is blocked  
**SO THAT** UI remains consistent  
**AS MEASURED BY** `CurrentPage == Execution`

## 7.2 Disable All Execution Buttons During Execution
**GIVEN** the user clicks Run, Explain, or Trace  
**WHEN** execution begins  
**THEN** all three execution buttons become disabled  
**SO THAT** no parallel or overlapping executions can occur  
**AS MEASURED BY** `PipelineExecutionViewModel.IsRunning == true` and all execution commands reporting `CanExecute == false`

## 7.3 Allow Navigation After Completion
**GIVEN** execution has completed  
**WHEN** user clicks Home/Editor/Settings  
**THEN** navigation succeeds  
**SO THAT** workflow continues  
**AS MEASURED BY** `CurrentPage != Execution`

## 7.4 Re‑Enable Execution Buttons After Completion or Error
**GIVEN** execution is running  
**WHEN** either `final_result_ready` or `pipeline_failed` arrives  
**THEN** Run, Explain, and Trace buttons re‑enable  
**SO THAT** the user can begin a new execution or navigate away  
**AS MEASURED BY** `PipelineExecutionViewModel.IsRunning == false` and all execution commands reporting `CanExecute == true`

---

# 8. Error Navigation Scenarios

## 8.1 Pipeline Failure Does Not Navigate
**GIVEN** execution is running  
**WHEN** `pipeline_failed` arrives  
**THEN** UI stays on Execution Page  
**SO THAT** user sees error context  
**AS MEASURED BY** `CurrentPage == Execution`

## 8.2 Navigation Allowed After Error
**GIVEN** pipeline failed  
**WHEN** user clicks Editor  
**THEN** navigation succeeds  
**SO THAT** user can fix CNL  
**AS MEASURED BY** `CurrentPage == Editor`

## 8.3 Transport Error Does Not Navigate
**GIVEN** execution is running  
**WHEN** WebSocket disconnects  
**THEN** UI stays on Execution Page  
**SO THAT** user sees transport failure  
**AS MEASURED BY** `CurrentPage == Execution`

---

# 9. Settings Navigation Scenarios

## 9.1 Navigate to Settings
**GIVEN** user is on any page  
**WHEN** they click the gear icon  
**THEN** UI navigates to Settings Page  
**SO THAT** backend configuration can be updated  
**AS MEASURED BY** `CurrentPage == Settings`

## 9.2 Block Settings During Execution
**GIVEN** execution is running  
**WHEN** user clicks Settings  
**THEN** navigation is blocked  
**SO THAT** execution remains stable  
**AS MEASURED BY** `CurrentPage == Execution`

## 9.3 Return From Settings
**GIVEN** user is on Settings Page  
**WHEN** they click Home or Editor  
**THEN** navigation succeeds  
**SO THAT** workflow continues  
**AS MEASURED BY** `CurrentPage != Settings`

---

# 10. Correlation‑ID Navigation Scenarios

## 10.1 Ignore Events From Old Executions
**GIVEN** active correlation ID = `abc-123`  
**WHEN** event arrives with `xyz-999`  
**THEN** UI ignores the event  
**SO THAT** navigation remains stable  
**AS MEASURED BY** unchanged `CurrentPage`

## 10.2 Reset Navigation on New Execution
**GIVEN** previous execution completed  
**WHEN** new `pipeline_started` arrives  
**THEN** UI navigates to Execution Page  
**SO THAT** user sees new pipeline progress  
**AS MEASURED BY** `CurrentPage == Execution`

---

# 11. Non‑Goals

These scenarios do **not** cover:

- parallel executions  
- queued executions  
- cancellation  
- plugin pages  
- multi‑file workflows  
- nondeterministic animations  

---

# Summary

These BDD navigation scenarios define all deterministic routing behavior in the Limelight‑X UI under the streaming API.  
They ensure predictable page transitions, strict execution locks, stable error handling, and correct incremental updates throughout the entire workflow.