# UI ViewModels (Streaming Edition)

## Purpose
This document defines all ViewModels used by the Limelight‑X UI.  
It specifies their responsibilities, state models, commands, and deterministic behavior.  
This specification is authoritative.  
All implementation must follow this architecture exactly.

This version incorporates the **event‑streaming API** defined in `api.md`, replacing single‑response HTTP calls with incremental JSON events delivered over WebSocket.

The UI operates in **app‑wide single‑execution mode**:
- Only one pipeline execution may be active at a time, across all open tabs.
- A tab's Run/Explain buttons are disabled while any tab (including itself) has an execution in flight.
- A new execution cannot begin until the previous one completes or fails, regardless of which tab started it.
- Each `.llx` tab nonetheless retains its own independent execution *results* (inspector state) between executions — the lock is about concurrency, not about sharing state across tabs.

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

4. **App‑Wide Single Execution**  
   - Only one `correlation_id` is active at a time, app‑wide.  
   - UI disables execution commands on every tab until the active pipeline finishes.  
   - No queuing, no cancellation, no parallel requests.

---

# 2. ViewModel Overview

The UI defines the following ViewModels:

- `WorkspaceViewModel`
- `FileTreeViewModel`
- `TabViewModel` (base), `CnlTabViewModel`, `PlainTextTabViewModel`
- `EditorViewModel` (per `CnlTabViewModel`)
- `PlainTextEditorViewModel` (per `PlainTextTabViewModel`)
- `PipelineExecutionViewModel` (per `CnlTabViewModel`)
- `IExecutionLockService`
- `SettingsViewModel`
- `AboutViewModel`
- Inspector ViewModels (per `CnlTabViewModel`):
  - `RawAstViewModel`  
  - `NormalizedAstViewModel`  
  - `IrViewModel`  
  - `PromptViewModel`  
  - `ModelOutputViewModel`  
  - `FinalResultViewModel`

Each ViewModel is deterministic and state‑derived.

---

# 3. WorkspaceViewModel

### Responsibilities
- Holds the open root folder path.
- Owns the folder tree (`FileTreeViewModel`) and the collection of open tabs.
- Opens or focuses a tab when a file is selected in the tree.
- Coordinates the Settings‑modal and About‑modal open/close gates.
- Creates untitled tabs and resolves file paths for Save/Save As/Save All.

### State
- `RootFolderPath : string?`
- `OpenTabs : ObservableCollection<TabViewModel>`
- `ActiveTab : TabViewModel?`
- `IsSettingsOpen : bool`
- `IsAboutOpen : bool`

### Commands
- `OpenFolderCommand`
- `OpenOrFocusTabCommand(FileTreeNodeViewModel)`
- `CloseTabCommand(TabViewModel)`
- `OpenSettingsCommand`
- `CloseSettingsCommand`
- `OpenAboutCommand`
- `CloseAboutCommand`
- `NewLlxFileCommand`
- `NewTxtFileCommand`
- `OpenFileCommand`
- `SaveCommand`
- `SaveAsCommand`
- `SaveAllCommand`

### Rules
- Must not depend on pipeline state except: `OpenSettingsCommand` is blocked while `IExecutionLockService.IsAnyExecutionRunning == true` (see §8).
- `OpenAboutCommand` is **never** blocked by execution state — explicit contrast with `OpenSettingsCommand`; About has no backend side effects.
- Tab open/focus/close and folder browsing are never gated by execution state.
- Closing a dirty tab (or closing Settings with unsaved changes) triggers the same unsaved‑changes confirmation dialog (see `ui-error-handling.md` and `ui-components.md`'s `ModalService` usage).
- `NewLlxFileCommand` creates a new `CnlTabViewModel`, and `NewTxtFileCommand` a new `PlainTextTabViewModel`, both with `IsUntitled = true`, `FilePath = null`, and `Header` set from a single shared session‑scoped counter (`Untitled-1`, `Untitled-2`, …) incremented across both commands — not two independent counters. The new tab starts with `IsDirty = false` (no unsaved‑changes prompt until the user actually edits it).
- `OpenFileCommand` opens an unfiltered "open any file" picker and dispatches the chosen path through the same `.llx`→`CnlTabViewModel` / else→`PlainTextTabViewModel` rule already used by `OpenOrFocusTabCommand` (§4), independent of whether a folder (`RootFolderPath`) is open.
- `SaveCommand` on an untitled tab (`IsUntitled == true`) behaves exactly like `SaveAsCommand`: prompts for a location, then writes and clears `IsUntitled`/sets `FilePath`/`IsDirty = false`. On a non‑untitled tab it writes directly to the existing `FilePath` and sets `IsDirty = false`.
- `SaveAsCommand` always prompts for a location, regardless of whether the tab is untitled, then writes and updates `FilePath`/`IsDirty = false` (and clears `IsUntitled` if it was set).
- `SaveAllCommand` iterates every open tab with `IsDirty == true`, applying `SaveCommand`'s per‑tab resolution (direct write, or prompt if untitled) to each. If the user cancels an individual tab's save‑location prompt, that tab is skipped and the command continues to the remaining dirty tabs rather than aborting.
- `SaveCommand`/`SaveAsCommand`/`SaveAllCommand` are enabled only when applicable (see `ui-components.md` §3.5 Rules for exact enablement conditions).

### File & Save Picker Behavior
- Opening a file (`OpenFileCommand`) requires an unfiltered file picker capability — broader than today's `.llx`‑only `PickCnlFileAsync()`.
- Saving (`SaveCommand`/`SaveAsCommand` when prompting) requires a save‑file picker that suggests a file name and default extension based on the tab kind (`.llx` for `CnlTabViewModel`, no forced extension for `PlainTextTabViewModel`).
- These are new picker capabilities beyond today's `PickCnlFileAsync()`/`PickFolderAsync()` — this document describes the required behavior; the underlying picker/service abstraction is an implementation detail.

---

# 4. FileTreeViewModel

### Responsibilities
- Recursively scans the open root folder (pure client‑side filesystem read; no backend endpoint is involved).
- Tracks expand/collapse state per node.
- Emits the selected file to `WorkspaceViewModel.OpenOrFocusTabCommand`.

### State
- `RootPath : string?`
- `Nodes : ObservableCollection<FileTreeNodeViewModel>`
- `SelectedNode : FileTreeNodeViewModel?`

### FileTreeNodeViewModel
- `Name : string`
- `FullPath : string`
- `IsDirectory : bool`
- `IsExpanded : bool`
- `Children : ObservableCollection<FileTreeNodeViewModel>`

### Rules
- Must not perform any pipeline or backend call.
- Must surface filesystem read errors (e.g. permission denied) immediately, the same as any other `UiError`.

---

# 5. TabViewModel Family

### 5.1 TabViewModel (base)
- `Header : string` (file name, or `Untitled-N` while `IsUntitled`)
- `FilePath : string?` (`null` while `IsUntitled`)
- `IsUntitled : bool` — `true` for tabs created via New LLX File/New TXT File that have never been saved
- `IsDirty : bool`
- `CloseCommand`

### 5.2 CnlTabViewModel : TabViewModel
- Owns one `EditorViewModel` instance (§6).
- Owns one `PipelineExecutionViewModel` instance (§7), which in turn owns the six inspector ViewModels (§11) for this tab only.
- Instantiated when a `.llx` file is opened, **or** when `NewLlxFileCommand` (§3) creates a new untitled tab; disposed (along with its owned `EditorViewModel`/`PipelineExecutionViewModel`/inspectors) when the tab closes.

### 5.3 PlainTextTabViewModel : TabViewModel
- Owns one `PlainTextEditorViewModel` instance.
- Instantiated when a non‑`.llx` file is opened, **or** when `NewTxtFileCommand` (§3) creates a new untitled tab.

### 5.4 PlainTextEditorViewModel
- `Text : string`
- `CursorPosition : int`
- `IsDirty : bool`
- No validation, no syntax highlighting, no pipeline commands.

### Rules
- `EditorViewModel` and `PipelineExecutionViewModel` are **per‑tab instances**, not composition‑root singletons — each `.llx` tab gets its own pair, constructed when the tab opens.
- A tab's own inspector/result state is retained across the tab's lifetime and is unaffected by other tabs' executions.

---

# 6. EditorViewModel (per `.llx` tab)

### Responsibilities
- Holds CNL text for this tab.
- Performs live validation via `/explain`.
- Provides execution commands for this tab.

### State
- `SourceText : string`
- `SyntaxErrors : ObservableCollection<EditorError>`
- `CanExecute : bool` (derived: `!IExecutionLockService.IsAnyExecutionRunning && SourceText not empty`)

`EditorViewModel` has no `IsExecuting` property of its own. Two distinct flags matter here and must not be confused:
- `PipelineExecutionViewModel.IsRunning` (§7) — is *this tab's* execution currently running (drives this tab's own spinner/inspector state).
- `IExecutionLockService.IsAnyExecutionRunning` (§8) — is *any* tab's execution currently running, app‑wide (drives `CanExecute` on every tab and the Settings gear).

### Commands
- `RunCommand` — invokes `POST /trace`.
- `ExplainCommand` — invokes `POST /explain`.

There is no `TraceCommand`. The Trace button and its distinct trigger are removed entirely; Run now performs what Trace previously did.

### Execution Behavior (App‑Wide Single Execution)
1. Send HTTP request (`POST /trace` for Run, `POST /explain` for Explain).
2. Receive `correlation_id`.
3. Notify this tab's `PipelineExecutionViewModel` to begin streaming — this sets `IsRunning = true` (§7, on `pipeline_started`) and acquires the app‑wide lock via `IExecutionLockService.TryAcquire` (§8), which disables execution buttons on every tab and the Settings gear via `CanExecute`.
4. No workspace‑area navigation occurs; the result renders in place in this tab's execution panel.
5. Execution buttons re‑enable everywhere automatically once `IExecutionLockService.IsAnyExecutionRunning` recomputes to `false`, which happens when:
   - `final_result_ready` arrives, or  
   - `pipeline_failed` arrives.

### Live Validation
- Triggered on text change.
- Invokes `/explain` and subscribes to its event sequence (`pipeline_started` → `raw_ast_generated` → `normalized_ast_generated`) exactly like any other execution — there is no separate non-streaming mode.
- Live validation is **exempt from `IExecutionLockService`** — it does not acquire the app‑wide lock and is not blocked by another tab's in‑flight Run/Explain, matching today's behavior where validation is a wholly separate correlation‑id track from the toolbar commands.
- Does **not** render in this tab's execution panel: this is the one case where a `/explain` execution's events update `SyntaxErrors` in place on the editor instead of driving `PipelineExecutionViewModel`'s inspectors.
- Updates `SyntaxErrors` as the sequence completes (at `normalized_ast_generated`, or on `pipeline_failed`).

---

# 7. PipelineExecutionViewModel (per `.llx` tab)

### Responsibilities
- Holds this tab's pipeline execution state.
- Receives streaming events from WebSocket, filtered to this tab's active `correlation_id`.
- Updates this tab's inspector ViewModels incrementally.
- Tracks this tab's execution status and errors.
- Clears this tab's state when a new pipeline begins in this tab.

### State
- `CorrelationId : string`
- `IsRunning : bool` — this tab's own execution-state flag (see §6 for how it differs from the app‑wide lock). Drives this tab's execution progress indicator (`ui-components.md` §4.4) — shown while `true`, hidden while `false`. This is a per-tab signal, distinct from the app-wide `IExecutionLockService.IsAnyExecutionRunning` that gates `CanExecute` on every tab (§6).
- `HasErrors : bool`
- Inspector ViewModels (this tab's own instances):
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
- Clear this tab's inspector ViewModels.
- Set `IsRunning = true`.
- Set `HasErrors = false`.
- Acquire the app‑wide lock via `IExecutionLockService.TryAcquire(this tab)`.

#### `raw_ast_generated`
- Update `RawAstViewModel`.
- Expand Raw AST panel automatically.

#### `normalized_ast_generated`
- Update `NormalizedAstViewModel`.

#### `ir_generated`
- Update `IrViewModel`.

#### `prompt_generated`
- Append the incoming prompt to `PromptViewModel.Prompts`. This event may fire multiple times per execution — once per model-calling IR operation, in program order — so it must append rather than replace the collection.

#### `model_output_generated`
- Append the incoming output to `ModelOutputViewModel.Outputs`. Like `prompt_generated`, this event may fire multiple times per execution and must append rather than replace.

#### `final_result_ready`
- Update `FinalResultViewModel`.
- Set `IsRunning = false`.
- Release the app‑wide lock via `IExecutionLockService.Release(this tab)`.

#### `pipeline_failed`
- Set `HasErrors = true`.
- Populate this tab's error banner.
- Set `IsRunning = false`.
- Release the app‑wide lock via `IExecutionLockService.Release(this tab)`.

### Rules
- Must ignore events whose `correlation_id` does not match this tab's active execution.
- Must not reorder events.
- Must not buffer events.
- Must not retry events.
- Must not reconstruct pipeline stages manually.
- If this tab is closed while its execution is still in flight, `IExecutionLockService` must still be released — either immediately on close, or upon the terminal event if the underlying HTTP/WebSocket exchange is left to finish in the background. Implementations must pick one and apply it consistently; the lock must never be left permanently held by a closed tab.

---

# 8. IExecutionLockService

### Responsibilities
- Tracks whether any tab, app‑wide, currently has an execution in flight.
- Gates every tab's `EditorViewModel.CanExecute` and the Settings gear (`WorkspaceViewModel.OpenSettingsCommand`).
- Does **not** gate tab switching, tab open/close, or folder‑tree browsing — those remain available regardless of lock state.

### State
- `IsAnyExecutionRunning : bool`

### Members
- `TryAcquire(tabId) : bool` — succeeds only if no other tab currently holds the lock.
- `Release(tabId)`
- `ExecutionLockChanged` event — raised whenever `IsAnyExecutionRunning` changes, so every tab's `CanExecute` and the Settings gear can recompute.

### Rules
- Exactly one tab may hold the lock at a time.
- Live validation (`/explain` triggered by text change, §6) never calls `TryAcquire` — it is exempt from this lock.
- This is a lock, not an execution engine: it does not multiplex or track multiple concurrent correlation IDs. Each `PipelineExecutionViewModel` remains solely responsible for its own tab's event stream (§7).

---

# 9. SettingsViewModel

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
- Must surface backend startup errors: if the `llx serve` relaunch fails, synthesize an `api`-category `UiError`, show it via `ErrorBannerViewModel.IsVisible = true`, and keep the Settings modal open (see `ui-error-handling.md` §7.5). The previous backend connection, if any, is left running until a restart succeeds.
- `LogPath` empty/unset resolves to `config.json`'s own directory (`%APPDATA%\LimelightX\`); a non-empty `LogPath` must be an absolute path (existing validation, unchanged) and is used as the log directory instead. Either way the log file itself is always named `Limelight-x-log.txt` (`ui-deployment.md` §4.3). This resolution is not persisted back into `config.json` — an empty `LogPath` stays empty until the user explicitly sets a custom one.
- A successful Save redirects logging to the new `LogPath` immediately — the same restart-on-success moment that already re-points the backend connection (see above). No further entries are written to the previous log location once the redirect completes.
- `SettingsViewModel` is a single composition‑root instance (not per‑tab) — Settings is not file‑scoped.

---

# 10. AboutViewModel

### Responsibilities
- Holds read‑only project information for the About modal.
- Closes the About modal.

### State
- `AppName : string` — immutable
- `Description : string` — immutable, the project description shown in the modal
- `Version : string` — immutable, sourced from the assembly version at runtime (see `ui-build-pipeline.md`)
- `GitHubUrl : string` — immutable, `https://github.com/dondemcsak/limelight-x`

### Commands
- `CloseCommand` — delegates to `WorkspaceViewModel.CloseAboutCommand` (§3).

### Rules
- `AboutViewModel` is a single composition‑root instance (not per‑tab) — About is not file‑scoped, mirroring §9 SettingsViewModel.
- Never gated by `IExecutionLockService` — explicit contrast with §9.
- Activating the GitHub link opens the system default browser out‑of‑process; it does not navigate within the app.

---

# 11. Inspector ViewModels

Each inspector ViewModel is responsible for:

- deterministic state  
- collapse/expand state  
- formatted display text  
- incremental updates from streaming events  

Each is instantiated once per `.llx` tab (owned by that tab's `PipelineExecutionViewModel`, §7), not app‑wide.

### 11.1 RawAstViewModel
State:
- `AstNodes : ObservableCollection<AstNode>`
- `IsCollapsed : bool`
- `HasErrors : bool` — set when this inspector's data fails to render; backs `InspectorErrorPanel` (`ui-components.md` §6.2) via an `InspectorErrorViewModel`

### 11.2 NormalizedAstViewModel
State:
- `NormalizedNodes : ObservableCollection<NormalizedAstNode>`
- `IsCollapsed : bool`
- `HasErrors : bool`

### 11.3 IrViewModel
State:
- `Operations : ObservableCollection<IrOperation>`
- `IsCollapsed : bool`
- `HasErrors : bool`

### 11.4 PromptViewModel
State:
- `Prompts : ObservableCollection<Prompt>` — grows by one entry per `prompt_generated` event (one event per model-calling IR operation), rather than being set all at once.
- `IsCollapsed : bool`
- `HasErrors : bool`

### 11.5 ModelOutputViewModel
State:
- `Outputs : ObservableCollection<ModelOutput>` — grows by one entry per `model_output_generated` event (one event per model-calling IR operation), rather than being set all at once.
- `IsCollapsed : bool`
- `HasErrors : bool`

### 11.6 FinalResultViewModel
State:
- `ResultText : string`
- `ContentType : string`
- `IsCollapsed : bool`

### Rules
- Inspector ViewModels must never perform pipeline logic.
- They must only reflect streamed event data for their owning tab.
- They must clear state, including `HasErrors`, when `pipeline_started` arrives for their tab.
- `HasErrors` is set locally by the inspector when it fails to render its own data (e.g. a malformed node it cannot display) — it is independent of `PipelineExecutionViewModel.HasErrors`, which reflects a `pipeline_failed` event. An inspector can have `HasErrors = true` from a local rendering failure even on an otherwise-successful pipeline run.

---

# 12. Deterministic State Rules

### Allowed State
- collapse/expand  
- open tabs and tab order  
- active tab  
- expanded/collapsed folder tree nodes  
- editor cursor position  
- current correlation_id (per tab)  
- inspector contents (per tab)  
- whether any execution is in flight app‑wide (`IExecutionLockService.IsAnyExecutionRunning`)  
- whether the Settings modal is open (`IsSettingsOpen`) or the About modal is open (`IsAboutOpen`)  
- per‑tab untitled state (`IsUntitled`, `FilePath`)

### Forbidden State
- implicit transitions  
- nondeterministic animations  
- hidden state machines  
- state not represented in ViewModels  
- buffering or reordering events

---

# 13. Error Handling

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
- file open/save (e.g. permission denied, disk full, invalid path)

### Rules
- Errors must appear immediately.
- Errors must be human‑readable.
- Errors must be surfaced in:
  - that tab's error banner  
  - that tab's execution panel  
  - that tab's inspector panels (if applicable)  
  - inline editor (validation errors)
- Each tab's `ErrorBannerViewModel` clears when a new `pipeline_started` event arrives for that tab (per §7), or when the user dismisses it — see `ui-error-handling.md` §8. Switching tabs never clears another tab's banner.
- A failed Open File, Save, Save As, or Save All leaves the affected tab's state unchanged (still dirty/untitled as before the attempt) and surfaces the failure via that tab's error banner; it does not partially update `FilePath`/`IsDirty`/`IsUntitled`.

---

# 14. Non‑Goals

ViewModels do **not** support:

- parallel pipeline executions  
- queued executions  
- cancellation  
- plugin inspectors  
- direct Rust integration  
- nondeterministic behavior  
- reconstructing pipeline stages  
- buffering or reassembling event streams
- autosave

---

# 15. Future Extensions

Potential enhancements:

- queued execution mode  
- cancelable pipelines  
- richer inspector interactions  
- visual IR graph  
- per‑tab independent execution (superseding `IExecutionLockService`'s app‑wide lock)  
- additional observability events (timing, resource usage)
- autosave for dirty/untitled tabs

---

# Summary

The Limelight‑X ViewModel layer is deterministic, MVVM‑pure, and fully aligned with the streaming API.  
Pipeline results arrive incrementally over WebSocket and update the owning tab's inspector ViewModels in real time.  
App‑wide single‑execution mode ensures predictable behavior and simplifies state management, while each `.llx` tab retains its own independent execution results.  
`WorkspaceViewModel` also owns untitled‑tab creation and Save/Save As/Save All resolution, and coordinates the Settings and About modals — the latter never gated by execution state.  
All ViewModels are state‑derived, spec‑driven, and free of nondeterministic logic.
