# UI ViewModels (Streaming Edition)

## Purpose
This document defines all ViewModels used by the Limelight‑X UI.  
It specifies their responsibilities, state models, commands, and deterministic behavior.  
This specification is authoritative.  
All implementation must follow this architecture exactly.

This version incorporates the **event‑streaming API** defined in `api.md`, replacing single‑response HTTP calls with incremental JSON events delivered over WebSocket.

The UI operates in **strict single‑execution mode**:
- Only one pipeline execution may be active at a time.
- Run/Explain/Trace buttons are disabled while a pipeline is running.
- A new execution cannot begin until the previous one completes or fails.

---

# 1. Architectural Principles

1. **MVVM Purity**  
   - Views contain no logic.  
   - ViewModels contain state and commands.  
   - Services handle HTTP + WebSocket communication.

2. **Deterministic State**  
   - All UI state is derived from ViewModels.  
   - No hidden transitions.  
   - No nondeterministic behavior.

3. **Streaming Pipeline Model**  
   - Each pipeline execution produces a sequence of JSON events.  
   - ViewModels update incrementally as events arrive.  
   - Inspectors appear when their corresponding event arrives.

4. **Strict Single Execution**  
   - Only one `correlation_id` exists at a time.  
   - UI disables execution commands until the pipeline finishes.  
   - No queuing, no cancellation, no parallel requests.

---

# 2. ViewModel Overview

The UI defines the following ViewModels:

- `NavigationViewModel`
- `FileLoaderViewModel`
- `EditorViewModel`
- `PipelineExecutionViewModel`
- `SettingsViewModel`
- Inspector ViewModels:
  - `RawAstViewModel`
  - `NormalizedAstViewModel`
  - `IrViewModel`
  - `PromptViewModel`
  - `ModelOutputViewModel`
  - `FinalResultViewModel`

Each ViewModel is deterministic and state‑derived.

---

# 3. NavigationViewModel

### Responsibilities
- Controls routing between pages.
- Holds the current page.
- Exposes navigation commands.

### State
- `CurrentPage` (enum: Home, Editor, Execution, Settings)

### Commands
- `NavigateHomeCommand`
- `NavigateEditorCommand`
- `NavigateExecutionCommand`
- `NavigateSettingsCommand`

### Rules
- Navigation must be deterministic.
- Navigation must not depend on pipeline state except:
  - When a pipeline starts → navigate to ExecutionPage.
  - When a pipeline fails → remain on ExecutionPage and show errors.

---

# 4. FileLoaderViewModel

### Responsibilities
- Opens `.llx` files.
- Tracks recent files.
- Emits file content to `EditorViewModel`.

### State
- `RecentFiles : ObservableCollection<string>`
- `SelectedFilePath : string`
- `FileContent : string`

### Commands
- `OpenFileCommand`

### Rules
- Must validate file existence.
- Must surface file errors immediately.
- Must not modify editor state directly; only emit content.

---

# 5. EditorViewModel

### Responsibilities
- Holds CNL text.
- Performs live validation via `/explain`.
- Provides execution commands.

### State
- `SourceText : string`
- `SyntaxErrors : ObservableCollection<EditorError>`
- `CanExecute : bool` (derived: `!PipelineExecutionViewModel.IsRunning && SourceText not empty`)

`EditorViewModel` has no `IsExecuting` property of its own — `PipelineExecutionViewModel.IsRunning` (§6) is the single canonical execution-state flag. Every button/nav gate that needs to know whether a pipeline is active binds to it directly.

### Commands
- `RunCommand`
- `ExplainCommand`
- `TraceCommand`

### Execution Behavior (Strict Single Execution)
1. Send HTTP request (`POST /run`, `/explain`, or `/trace`).
2. Receive `correlation_id`.
3. Notify `PipelineExecutionViewModel` to begin streaming — this sets `IsRunning = true` (§6, on `pipeline_started`), which disables all execution buttons via `CanExecute`.
4. Navigate to ExecutionPage.
5. Execution buttons re‑enable automatically once `CanExecute` recomputes, which happens when:
   - `final_result_ready` arrives, or  
   - `pipeline_failed` arrives.

### Live Validation
- Triggered on text change.
- Invokes `/explain` and subscribes to its event sequence (`pipeline_started` → `raw_ast_generated` → `normalized_ast_generated`) exactly like any other execution — there is no separate non-streaming mode.
- Does **not** navigate to ExecutionPage: this is the one case where a `/explain` execution's events update `SyntaxErrors` in place on the Editor Page instead of driving `PipelineExecutionViewModel`'s inspectors.
- Updates `SyntaxErrors` as the sequence completes (at `normalized_ast_generated`, or on `pipeline_failed`).

---

# 6. PipelineExecutionViewModel

### Responsibilities
- Holds all pipeline execution state.
- Receives streaming events from WebSocket.
- Updates inspector ViewModels incrementally.
- Tracks execution status and errors.
- Clears state when a new pipeline begins.

### State
- `CorrelationId : string`
- `IsRunning : bool` — the single canonical execution-state flag; every other ViewModel (e.g. `EditorViewModel.CanExecute`, navigation guards) binds to this rather than keeping its own copy.
- `HasErrors : bool`
- `PipelineEvents : ObservableCollection<PipelineEvent>` — a display-only history backing the `ExecutionTimeline` component (`ui-components.md`); it is populated for UI rendering purposes only and is not part of the delivery pipeline. It does not violate the "no buffering or reordering" rule in §9/§11: that rule governs how incoming events are applied to state (in order, as received, without holding any back), not whether a read-only log of already-applied events is kept for display.
- Inspector ViewModels:
  - `RawAst : RawAstViewModel`
  - `NormalizedAst : NormalizedAstViewModel`
  - `Ir : IrViewModel`
  - `Prompts : PromptViewModel`
  - `ModelOutputs : ModelOutputViewModel`
  - `FinalResult : FinalResultViewModel`

### Commands
None.  
All updates come from streaming events.

### Event Handling

#### `pipeline_started`
- Clear all inspector ViewModels.
- Set `IsRunning = true`.
- Set `HasErrors = false`.

#### `raw_ast_generated`
- Update `RawAstViewModel`.
- Expand Raw AST panel automatically.

#### `normalized_ast_generated`
- Update `NormalizedAstViewModel`.

#### `ir_generated`
- Update `IrViewModel`.

#### `prompts_generated`
- Update `PromptViewModel`.

#### `model_outputs_generated`
- Update `ModelOutputViewModel`.

#### `final_result_ready`
- Update `FinalResultViewModel`.
- Set `IsRunning = false`.

#### `pipeline_failed`
- Set `HasErrors = true`.
- Populate error banner.
- Set `IsRunning = false`.

### Rules
- Must ignore events whose `correlation_id` does not match the active execution.
- Must not reorder events.
- Must not buffer events.
- Must not retry events.
- Must not reconstruct pipeline stages manually.

---

# 7. SettingsViewModel

### Responsibilities
- Holds backend configuration.
- Validates input.
- Applies changes by relaunching `llx serve`.

### State
- `BackendPort : int`
- `ApiKey : string`
- `LogPath : string` — the directory the persistent log file is written to, not just a validated string (see "Rules" below and `ui-deployment.md` §4.3)
- `EnvironmentProfile : string`
- `IsValid : bool`

### Commands
- `SaveSettingsCommand`

### Rules
- Must block Save while invalid.
- Must restart backend deterministically.
- Must surface backend startup errors: if the `llx serve` relaunch fails, synthesize an `api`-category `UiError`, show it via `ErrorBannerViewModel.IsVisible = true`, and keep the user on the Settings Page (see `ui-error-handling.md` §7.5). The previous backend connection, if any, is left running until a restart succeeds.
- `LogPath` empty/unset resolves to `config.json`'s own directory (`%APPDATA%\LimelightX\`); a non-empty `LogPath` must be an absolute path (existing validation, unchanged) and is used as the log directory instead. Either way the log file itself is always named `Limelight-x-log.txt` (`ui-deployment.md` §4.3). This resolution is not persisted back into `config.json` — an empty `LogPath` stays empty until the user explicitly sets a custom one.
- A successful Save redirects logging to the new `LogPath` immediately — the same restart-on-success moment that already re-points the backend connection (see above). No further entries are written to the previous log location once the redirect completes.

---

# 8. Inspector ViewModels

Each inspector ViewModel is responsible for:

- deterministic state  
- collapse/expand state  
- formatted display text  
- incremental updates from streaming events  

### 8.1 RawAstViewModel
State:
- `AstNodes : ObservableCollection<AstNode>`
- `IsCollapsed : bool`
- `HasErrors : bool` — set when this inspector's data fails to render; backs `InspectorErrorPanel` (`ui-components.md` §7.2) via an `InspectorErrorViewModel`

### 8.2 NormalizedAstViewModel
State:
- `NormalizedNodes : ObservableCollection<NormalizedAstNode>`
- `IsCollapsed : bool`
- `HasErrors : bool`

### 8.3 IrViewModel
State:
- `Operations : ObservableCollection<IrOperation>`
- `IsCollapsed : bool`
- `HasErrors : bool`

### 8.4 PromptViewModel
State:
- `Prompts : ObservableCollection<Prompt>`
- `IsCollapsed : bool`
- `HasErrors : bool`

### 8.5 ModelOutputViewModel
State:
- `Outputs : ObservableCollection<ModelOutput>`
- `IsCollapsed : bool`
- `HasErrors : bool`

### 8.6 FinalResultViewModel
State:
- `ResultText : string`
- `ContentType : string`
- `IsCollapsed : bool`

### Rules
- Inspector ViewModels must never perform pipeline logic.
- They must only reflect streamed event data.
- They must clear state, including `HasErrors`, when `pipeline_started` arrives.
- `HasErrors` is set locally by the inspector when it fails to render its own data (e.g. a malformed node it cannot display) — it is independent of `PipelineExecutionViewModel.HasErrors`, which reflects a `pipeline_failed` event. An inspector can have `HasErrors = true` from a local rendering failure even on an otherwise-successful pipeline run.

---

# 9. Deterministic State Rules

### Allowed State
- collapse/expand  
- selected tab  
- editor cursor position  
- current correlation_id  
- inspector contents  

### Forbidden State
- implicit transitions  
- nondeterministic animations  
- hidden state machines  
- state not represented in ViewModels  
- buffering or reordering events  

---

# 10. Error Handling

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
- Errors must appear immediately.
- Errors must be human‑readable.
- Errors must be surfaced in:
  - global error banner  
  - ExecutionPage  
  - inspector panels (if applicable)  
  - inline editor (validation errors)
- The global error banner (`ErrorBannerViewModel`) clears when a new `pipeline_started` event arrives (per §6), and also when the user navigates away from ExecutionPage (only possible once `IsRunning` is `false`, per `ui-routing-navigation.md` §7) — see `ui-error-handling.md` §8.

---

# 11. Non‑Goals

ViewModels do **not** support:

- parallel pipeline executions  
- queued executions  
- cancellation  
- plugin inspectors  
- multi‑file projects  
- direct Rust integration  
- nondeterministic behavior  
- reconstructing pipeline stages  
- buffering or reassembling event streams  

---

# 12. Future Extensions

Potential enhancements:

- queued execution mode  
- cancelable pipelines  
- richer inspector interactions  
- visual IR graph  
- multi‑file project support  
- additional observability events (timing, resource usage)

---

# Summary

The Limelight‑X ViewModel layer is deterministic, MVVM‑pure, and fully aligned with the streaming API.  
Pipeline results arrive incrementally over WebSocket and update inspector ViewModels in real time.  
Strict single‑execution mode ensures predictable behavior and simplifies state management.  
All ViewModels are state‑derived, spec‑driven, and free of nondeterministic logic.