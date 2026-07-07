# BDD — UI Workspace & Execution Concurrency (Streaming Edition)

## Purpose
This document defines all deterministic workspace scenarios for the Limelight‑X UI under the **event‑streaming API**.  
It specifies how the UI must behave across folder browsing, tab opening/switching/closing, execution, streaming, error handling, and Settings, now that the previous four‑page model has been replaced by a folder‑tree + tab‑strip workspace.

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
- tab switching, tab open/close, and folder browsing are never blocked by execution state

---

# 2. Workspace Areas

The UI contains four workspace areas:

1. **Explorer** — folder directory tree of the open root folder  
2. **Tab Strip** — one tab per open file  
3. **Tab Content Area** — the active tab's content, or a welcome/empty state  
4. **Settings** — a modal, opened via a persistent gear icon

The workspace shell is controlled exclusively by `WorkspaceViewModel`. There is no `PageType`/`CurrentPage` concept.

---

# 3. Startup Scenarios

## 3.1 Application Startup, No Prior Folder
**GIVEN** the application launches  
**AND** no folder was previously opened  
**WHEN** startup completes  
**THEN** the Tab Content Area shows the welcome/empty state with an "Open Folder" action  
**SO THAT** the user has an entry point  
**AS MEASURED BY** `WorkspaceViewModel.RootFolderPath == null` and `OpenTabs.Count == 0`

## 3.2 Application Startup, Folder Restored
**GIVEN** the application launches  
**AND** a folder was open in the previous session  
**WHEN** startup completes  
**THEN** the Explorer shows that folder's tree  
**SO THAT** the user resumes where they left off  
**AS MEASURED BY** `WorkspaceViewModel.RootFolderPath` equals the previously open folder

## 3.3 Startup With File (OS File Association)
**GIVEN** the application launches with a `.llx` file  
**WHEN** the file loads successfully  
**THEN** the file's containing folder opens in the Explorer  
**AND** a `CnlTabViewModel` for that file opens and becomes the active tab  
**SO THAT** the user can begin editing immediately  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab.FilePath` equals the opened file's path

---

# 4. Opening Files

## 4.1 Open a `.llx` File From the Tree
**GIVEN** the Explorer shows an open folder  
**WHEN** the user clicks a `.llx` file in the tree  
**THEN** a `CnlTabViewModel` opens (editor + execution panel) and becomes the active tab  
**SO THAT** the user can edit and execute CNL  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab is CnlTabViewModel`

## 4.2 Open a Plain Text File From the Tree
**GIVEN** the Explorer shows an open folder  
**WHEN** the user clicks a non‑`.llx` file in the tree  
**THEN** a `PlainTextTabViewModel` opens (generic text editor only) and becomes the active tab  
**SO THAT** the user can view/edit the file without CNL semantics  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab is PlainTextTabViewModel`

## 4.3 Re‑Open an Already‑Open File
**GIVEN** a file already has an open tab  
**WHEN** the user clicks that file again in the tree  
**THEN** the existing tab is focused, no duplicate tab is created  
**SO THAT** the workspace stays uncluttered  
**AS MEASURED BY** `WorkspaceViewModel.OpenTabs.Count` unchanged, `ActiveTab` set to the existing tab

---

# 5. Execution Scenarios (Streaming)

## 5.1 Run Executes In Place
**GIVEN** the user has a `.llx` tab active  
**WHEN** they click Run  
**THEN** `POST /trace` is invoked and results render inside that same tab's execution panel  
**SO THAT** the user sees full pipeline progress without leaving the tab  
**AS MEASURED BY** that tab's `PipelineExecutionViewModel.IsRunning == true`, no tab or workspace-area change

## 5.2 Explain Executes In Place
**GIVEN** the user has a `.llx` tab active  
**WHEN** they click Explain  
**THEN** `POST /explain` is invoked and Raw AST / Normalized AST render inside that same tab's execution panel  
**SO THAT** the user sees AST-level detail without leaving the tab  
**AS MEASURED BY** that tab's `RawAstViewModel`/`NormalizedAstViewModel` populate; `IrViewModel`/`PromptViewModel`/`ModelOutputViewModel`/`FinalResultViewModel` remain empty

---

# 6. Streaming Event Scenarios

## 6.1 Stay In Tab During Streaming
**GIVEN** execution is running in a tab  
**WHEN** any streaming event arrives  
**THEN** the event updates that tab's execution panel in place  
**SO THAT** pipeline progress is visible without navigation  
**AS MEASURED BY** the executing tab's inspector state updates; `WorkspaceViewModel.ActiveTab` is unaffected

Events include:
- `pipeline_started`  
- `raw_ast_generated`  
- `normalized_ast_generated`  
- `ir_generated`  
- `prompt_generated` (may appear more than once per sequence, unlike the other events listed here — once per model-calling IR operation)  
- `model_output_generated` (may appear more than once per sequence, unlike the other events listed here — once per model-calling IR operation)  
- `final_result_ready`

## 6.2 Switching Tabs During Streaming Is Allowed
**GIVEN** execution is running in tab A  
**WHEN** the user switches the active tab to tab B  
**THEN** the switch succeeds immediately  
**AND** tab A's execution continues streaming in the background, unaffected  
**SO THAT** the user can keep working while a long‑running pipeline executes  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab == tab B` and tab A's `PipelineExecutionViewModel.IsRunning` remains `true` until its terminal event

## 6.3 Final Result Does Not Force a Tab Switch
**GIVEN** `final_result_ready` arrives for tab A while tab B is active  
**WHEN** execution completes  
**THEN** the active tab remains tab B  
**SO THAT** the user is not interrupted  
**AS MEASURED BY** `WorkspaceViewModel.ActiveTab == tab B` unchanged

---

# 7. Execution Concurrency Lock Scenarios (App‑Wide Single Execution)

This section is the authoritative source for execution‑lock mechanics (when Run/Explain/Settings lock and unlock, and what remains free). `bdd-ui-error-cases.md` §7 covers only the error-triggered variant of these same rules and should be read as a special case of the rules here — if the two ever appear to disagree, this section wins.

## 7.1 Block New Execution In Other Tabs
**GIVEN** execution is running in tab A  
**WHEN** the user clicks Run or Explain in tab B  
**THEN** the click has no effect (the buttons are disabled)  
**SO THAT** only one execution is ever in flight  
**AS MEASURED BY** tab B's `EditorViewModel.CanExecute == false`

## 7.2 Disable Execution Buttons App‑Wide
**GIVEN** the user clicks Run or Explain in any tab  
**WHEN** execution begins  
**THEN** Run and Explain become disabled in **every** open tab  
**SO THAT** no parallel or overlapping executions can occur  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == true` and every tab's `CanExecute == false`

## 7.3 Tab Switching, Opening, and Closing Remain Allowed
**GIVEN** execution is running in some tab  
**WHEN** the user switches tabs, opens a new tab from the Explorer, or closes a different tab  
**THEN** the action succeeds immediately  
**SO THAT** the workspace stays fully usable during a long‑running execution  
**AS MEASURED BY** `WorkspaceViewModel.OpenTabs`/`ActiveTab` change as requested, independent of `IExecutionLockService.IsAnyExecutionRunning`

## 7.4 Settings Gear Disabled During Execution
**GIVEN** execution is running in some tab  
**WHEN** the user clicks the Settings gear icon  
**THEN** the Settings modal does not open (the gear is disabled)  
**SO THAT** a backend restart cannot abandon an in‑flight execution  
**AS MEASURED BY** `WorkspaceViewModel.IsSettingsOpen == false`

## 7.5 Re‑Enable After Completion or Error
**GIVEN** execution is running  
**WHEN** either `final_result_ready` or `pipeline_failed` arrives  
**THEN** Run and Explain re‑enable in every open tab, and the Settings gear re‑enables  
**SO THAT** the user can begin a new execution or open Settings  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning == false` and every tab's `CanExecute == true`

## 7.6 Progress Indicator Stays Tab‑Scoped, Not App‑Wide
**GIVEN** tab A is executing and tab B is open and idle  
**WHEN** tab A's execution is in flight  
**THEN** tab A's progress indicator is visible while tab B's Run/Explain are disabled (per §7.2) but tab B's own progress indicator remains hidden  
**SO THAT** users are never misled into thinking work is happening in a tab where nothing is running  
**AS MEASURED BY** tab A's `IsRunning == true` with its indicator visible, and tab B's `IsRunning == false` with its indicator hidden, simultaneously with tab B's `CanExecute == false`

---

# 8. Error Scenarios

## 8.1 Pipeline Failure Stays In Its Tab
**GIVEN** execution is running in a tab  
**WHEN** `pipeline_failed` arrives  
**THEN** that tab's error banner appears, in that tab  
**SO THAT** the user sees error context without a forced tab switch  
**AS MEASURED BY** that tab's `ErrorBannerViewModel.IsVisible == true`; `ActiveTab` unchanged

## 8.2 Execution Lock Released After Error
**GIVEN** a pipeline failed in some tab  
**WHEN** the user clicks Run/Explain in any tab  
**THEN** execution succeeds (the lock was released)  
**SO THAT** the user can retry or work elsewhere  
**AS MEASURED BY** `IExecutionLockService.IsAnyExecutionRunning` becoming `true` for the new execution

## 8.3 Transport Error Stays In Its Tab
**GIVEN** execution is running in a tab  
**WHEN** the WebSocket disconnects  
**THEN** that tab's error banner appears, in that tab  
**SO THAT** the user sees the transport failure without a forced tab switch  
**AS MEASURED BY** that tab's `PipelineExecutionViewModel.HasErrors == true`

---

# 9. Settings Scenarios

## 9.1 Open Settings
**GIVEN** no execution is in flight  
**WHEN** the user clicks the gear icon  
**THEN** the Settings modal opens  
**SO THAT** backend configuration can be updated  
**AS MEASURED BY** `WorkspaceViewModel.IsSettingsOpen == true`

## 9.2 Block Settings During Execution
**GIVEN** execution is running in some tab  
**WHEN** the user clicks the gear icon  
**THEN** the Settings modal does not open  
**SO THAT** execution remains stable  
**AS MEASURED BY** `WorkspaceViewModel.IsSettingsOpen == false`

## 9.3 Close Settings
**GIVEN** the Settings modal is open  
**WHEN** the user saves or cancels  
**THEN** the modal closes and the previously active tab is shown again  
**SO THAT** the workflow continues where it left off  
**AS MEASURED BY** `WorkspaceViewModel.IsSettingsOpen == false`

---

# 10. Correlation‑ID Scenarios

## 10.1 Ignore Events From Old Executions
**GIVEN** a tab's active correlation ID = `abc-123`  
**WHEN** an event arrives with `xyz-999`  
**THEN** the UI ignores the event  
**SO THAT** workspace state remains stable  
**AS MEASURED BY** unchanged tab/inspector state

## 10.2 New Execution Clears Only Its Own Tab
**GIVEN** a previous execution completed in tab A  
**WHEN** a new `pipeline_started` arrives for tab A  
**THEN** tab A's inspectors clear; tab B's inspectors (if any) are unaffected  
**SO THAT** each tab's results remain independent  
**AS MEASURED BY** tab A's inspector state resets; tab B's inspector state unchanged

---

# 11. Non‑Goals

These scenarios do **not** cover:

- parallel executions (per‑tab concurrent execution is a possible future extension)  
- queued executions  
- cancellation  
- plugin panels  
- nondeterministic animations  
- a persisted project/workspace manifest (open folder/tabs are session state only)

---

# Summary

These BDD workspace scenarios define all deterministic folder/tab behavior in the Limelight‑X UI under the streaming API.  
They ensure predictable tab opening/switching/closing, an app‑wide execution lock that never blocks workspace navigation, stable per‑tab error handling, and correct incremental updates throughout the entire workflow.
