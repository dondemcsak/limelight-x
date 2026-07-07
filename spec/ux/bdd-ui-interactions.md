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

## 4.1 Raw AST Appears
**GIVEN** execution is running  
**WHEN** `raw_ast_generated` arrives  
**THEN** RawAstPanel becomes visible  
**SO THAT** the user sees the first pipeline stage  
**AS MEASURED BY** `RawAstViewModel.AstNodes.Count > 0`

## 4.2 Normalized AST Appears
**GIVEN** raw AST is visible  
**WHEN** `normalized_ast_generated` arrives  
**THEN** NormalizedAstPanel becomes visible  
**SO THAT** the user sees normalized structure  
**AS MEASURED BY** `NormalizedAstViewModel.NormalizedNodes.Count > 0`

## 4.3 IR Appears
**GIVEN** normalized AST is visible  
**WHEN** `ir_generated` arrives  
**THEN** IrPanel becomes visible  
**SO THAT** the user sees compiled IR  
**AS MEASURED BY** `IrViewModel.Operations.Count > 0`

## 4.4 Prompts Appear
**GIVEN** IR is visible  
**WHEN** the first `prompt_generated` arrives  
**THEN** PromptPanel becomes visible  
**SO THAT** the user sees model prompts  
**AS MEASURED BY** `PromptViewModel.Prompts.Count > 0`

## 4.5 Model Outputs Appear
**GIVEN** prompts are visible  
**WHEN** the first `model_output_generated` arrives  
**THEN** ModelOutputPanel becomes visible  
**SO THAT** the user sees model responses  
**AS MEASURED BY** `ModelOutputViewModel.Outputs.Count > 0`

## 4.7 Multiple Prompt/Output Pairs Accumulate Across a Multi-Step Trace
**GIVEN** execution is running for a program with two model-calling operations (e.g. `Summarize` → `Translate`)  
**WHEN** both `prompt_generated`/`model_output_generated` pairs arrive in order  
**THEN** `PromptViewModel.Prompts` and `ModelOutputViewModel.Outputs` each grow by one entry per event, without being cleared or reset between the two pairs  
**SO THAT** the user sees every model call in a chained transformation, not just the last one  
**AS MEASURED BY** `PromptViewModel.Prompts.Count == 1` and `ModelOutputViewModel.Outputs.Count == 1` immediately after the first pair, and `PromptViewModel.Prompts.Count == 2` and `ModelOutputViewModel.Outputs.Count == 2` immediately after the second pair, with the first pair's entries still present and unchanged at both checkpoints

## 4.6 Final Result Appears
**GIVEN** execution is running  
**WHEN** `final_result_ready` arrives  
**THEN** FinalResultPanel becomes visible  
**SO THAT** the user sees the final pipeline output  
**AS MEASURED BY** `FinalResultViewModel.ResultText != null`

## 4.7 Re‑Enable Buttons App‑Wide Only After Completion or Error
**GIVEN** execution is running in some tab  
**WHEN** either  
- `final_result_ready` arrives, **or**  
- `pipeline_failed` arrives  
**THEN** Run and Explain re‑enable on every open tab  
**SO THAT** the user can start a new execution (in any tab) after the current one finishes  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == false` and `CanExecute == true` for Run and Explain on every tab

---

# 5. Collapse/Expand Interactions

## 5.1 Collapse Inspector
**GIVEN** an inspector panel is visible  
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

# 6. Workspace Interactions

## 6.1 Tab Switching Is Never Blocked By Execution
**GIVEN** execution is running in some tab  
**WHEN** the user switches to a different tab, opens a new tab, or closes a different tab  
**THEN** the action succeeds immediately  
**SO THAT** the UI remains fully usable during a long‑running execution  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab`/`OpenTabs` change as requested, independent of `IExecutionLockService.IsAnyExecutionRunning`

## 6.2 Settings Blocked, Tabs Unaffected, During Execution
**GIVEN** execution is running in some tab  
**WHEN** the user clicks the Settings gear icon  
**THEN** the Settings modal does not open  
**AND** tab switching remains fully available  
**SO THAT** only backend-affecting actions are gated, not workspace navigation  
**AS MEASURED BY** `WorkspaceViewModel.IsSettingsOpen == false` while `ActiveTab` changes freely

---

# 7. Error Interactions

## 7.1 Pipeline Failure
**GIVEN** execution is running  
**WHEN** `pipeline_failed` arrives  
**THEN** global error banner appears  
**SO THAT** the user sees the failure immediately  
**AS MEASURED BY** `ErrorBannerViewModel.IsVisible == true`

## 7.2 Inspector Error
**GIVEN** an inspector fails to render  
**WHEN** an inspector error occurs  
**THEN** InspectorErrorPanel appears  
**SO THAT** the user sees the failure context  
**AS MEASURED BY** `InspectorErrorViewModel.Message != null`

## 7.3 WebSocket Disconnect
**GIVEN** execution is running  
**WHEN** WebSocket disconnects  
**THEN** global error banner appears  
**SO THAT** the user sees transport failure  
**AS MEASURED BY** `HasErrors == true`

---

# 8. Settings Interactions

## 8.1 Invalid Settings Block Save
**GIVEN** the user enters invalid settings  
**WHEN** they click Save  
**THEN** Save is blocked  
**SO THAT** backend remains stable  
**AS MEASURED BY** `IsValid == false`

## 8.2 Valid Settings Restart Backend
**GIVEN** settings are valid  
**WHEN** the user clicks Save  
**THEN** backend restarts  
**SO THAT** new configuration applies  
**AS MEASURED BY** successful relaunch of `llx serve`

---

# 9. Logging Persistence

## 9.1 Default Location Write
**GIVEN** no custom `LogPath` is configured  
**WHEN** a `UiError` is added to any logged collection (`WorkspaceViewModel.Errors`, a tab's `EditorViewModel.ValidationErrors`, a tab's `PipelineExecutionViewModel.Errors`, `SettingsViewModel.Errors`)  
**THEN** an entry is appended to `%APPDATA%\LimelightX\Limelight-x-log.txt`  
**SO THAT** diagnostics are recoverable without a custom setup  
**AS MEASURED BY** the file's contents after the error occurs

## 9.2 Custom LogPath Write
**GIVEN** the user has configured a custom `LogPath`  
**WHEN** a `UiError` is logged  
**THEN** the entry is appended to `<LogPath>\Limelight-x-log.txt` instead of the default location  
**SO THAT** the user's chosen log directory is honored  
**AS MEASURED BY** the file's contents at `<LogPath>`, and the absence of any new entry at the default location

## 9.3 Append Across Sessions
**GIVEN** the log file already contains entries from a previous session  
**WHEN** the app restarts and a new error is logged  
**THEN** both the prior and the new entries are present in the file  
**SO THAT** diagnostic history survives restarts  
**AS MEASURED BY** the file never being truncated on startup

## 9.4 Write Failure Does Not Block The Original Error
**GIVEN** the log directory is unwritable  
**WHEN** a `UiError` occurs  
**THEN** the app does not crash and no additional user-facing error is raised for the failed write  
**SO THAT** a logging problem never masks or blocks the real error  
**AS MEASURED BY** the original error still appearing through its normal UI surface (banner/inline/inspector)

## 9.5 LogPath Change Redirects Immediately
**GIVEN** logging is currently active at the default location  
**WHEN** the user saves a new `LogPath` in Settings  
**THEN** subsequent entries are appended to the new location  
**SO THAT** the user doesn't need to restart the app for a log-path change to take effect  
**AS MEASURED BY** no further entries appearing at the old location after the save, and new entries appearing at `<new LogPath>\Limelight-x-log.txt`

---

# 10. Correlation‑ID Interactions

## 10.1 Ignore Mismatched Events
**GIVEN** active correlation ID = `abc-123`  
**WHEN** an event arrives with `xyz-999`  
**THEN** UI ignores the event  
**SO THAT** no cross‑execution contamination occurs  
**AS MEASURED BY** unchanged inspector state

## 10.2 Reset on New Execution
**GIVEN** previous execution completed  
**WHEN** new `pipeline_started` arrives  
**THEN** all inspector ViewModels clear  
**SO THAT** the UI begins a fresh execution  
**AS MEASURED BY** empty inspector panels

---

# 11. Non‑Goals

These interactions do **not** cover:

- parallel executions (per‑tab concurrent execution is a possible future extension)  
- queued executions  
- cancellation  
- plugin inspectors  
- nondeterministic animations  

---

# Summary

These BDD scenarios define all deterministic user interactions in the Limelight‑X UI under the streaming API.  
They ensure predictable execution workflows, incremental inspector updates, strict navigation constraints, and robust error handling — all aligned with the Limelight‑X architecture.