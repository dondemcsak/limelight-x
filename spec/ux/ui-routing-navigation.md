# UI Routing & Navigation (Streaming Edition)

## Purpose
This document defines the complete workspace‑shell and execution‑concurrency model for the Limelight‑X UI.  
It specifies folder/tab behavior, execution‑driven state, error routing, and deterministic behavior under the **event‑streaming API**.

This specification is authoritative.  
All implementation must follow this model exactly.

---

# 1. Architectural Principles

1. **Deterministic Workspace State**  
   - All tab/folder state transitions are explicit.  
   - No hidden transitions.  
   - No nondeterministic animations.

2. **MVVM‑Driven Shell**  
   - The workspace shell is controlled exclusively by `WorkspaceViewModel` (open folder, tabs, active tab).  
   - Views contain no routing/tab logic.

3. **In‑Place Execution**  
   - Starting a pipeline execution runs it in place, inside the tab that started it.  
   - There is no "navigate to an Execution Page" — the execution panel is always part of a `.llx` tab, populated or not.  
   - Streaming events update that tab's execution panel in real time.

4. **App‑Wide Single Execution**  
   - Only one pipeline execution may be active at a time, across the whole app.  
   - Starting a new execution is blocked while another tab's execution is in flight (see §7).  
   - Switching tabs, opening tabs, closing tabs, and browsing the folder tree are never blocked by an in‑flight execution.

5. **Error‑Driven Rendering**  
   - Pipeline errors (`pipeline_failed`) render in the owning tab's execution panel.  
   - That tab's error banner appears immediately.

---

# 2. Workspace Areas

The UI defines five workspace areas:

1. **MenuBar** (title‑bar row) — custom in‑window menu with File and Help top‑level menus (see `ui-components.md` §3.5)  
2. **Explorer** (left pane) — folder directory tree of the open root folder  
3. **Tab Strip** — one tab per open file, switchable, closable  
4. **Tab Content Area** — renders the active tab's content (`.llx` split view, plain text editor, or the welcome/empty state when no tabs are open)  
5. **Modals — Settings and About** — Settings is opened via a persistent gear icon or File > Settings; About is opened via Help > About; neither is a tab

Each of these is backed by a corresponding ViewModel (`WorkspaceViewModel`, `FileTreeViewModel`, `TabViewModel` family, `SettingsViewModel`, `AboutViewModel`) — see `ui-viewmodels.md`.

There is no `PageType` enum and no single `CurrentPage` property. The old four‑page model (Home/Editor/Execution/Settings) is retired in favor of the areas above.

---

# 3. WorkspaceViewModel

### Responsibilities
- Holds the open root folder path.
- Owns the folder tree and the open‑tabs collection.
- Opens or focuses a tab when a file is selected in the tree.
- Coordinates the app‑wide execution lock gate (via `IExecutionLockService`) and the Settings‑ and About‑modal gates.
- Creates untitled tabs and resolves Save/Save As/Save All.

### State
```csharp
string? RootFolderPath;
ObservableCollection<TabViewModel> OpenTabs;
TabViewModel? ActiveTab;
bool IsSettingsOpen;
bool IsAboutOpen;
```

### Commands
- `OpenFolderCommand`
- `OpenOrFocusTabCommand(FileTreeNodeViewModel)`
- `CloseTabCommand(TabViewModel)`
- `OpenSettingsCommand` / `CloseSettingsCommand`
- `OpenAboutCommand` / `CloseAboutCommand`
- `NewLlxFileCommand` / `NewTxtFileCommand`
- `OpenFileCommand`
- `SaveCommand` / `SaveAsCommand` / `SaveAllCommand`

### Rules
- Opening, focusing, or closing a tab is always synchronous and immediate — none of these actions are ever blocked by an in‑flight execution.
- Closing a tab with unsaved changes shows the existing unsaved‑changes confirmation dialog (same dialog used for Settings, generalized — see §6). Untitled/dirty tabs go through the same dialog on close.
- `OpenSettingsCommand` is blocked while `IExecutionLockService.IsAnyExecutionRunning == true` (see §7).
- `OpenAboutCommand` is never blocked by execution state (explicit contrast with `OpenSettingsCommand` — see §7.1).

---

# 4. Workspace Entry Points

### 4.1 Application Startup
- On launch, the UI restores the last‑opened root folder (if any) and its tree state.
- If no folder was previously opened, the Tab Content Area shows the welcome/empty state with an "Open Folder" action and a short recent‑folders list.
- If a `.llx` file is opened immediately (e.g., via OS file association), its containing folder is opened in the Explorer and the file's tab is opened and focused directly.

### 4.2 Opening a File
A file can be opened three ways, all converging on the same tab‑open logic:
- Clicking a file in the Explorer opens a tab (or focuses it, if already open) in the Tab Content Area.
- File > Open File (`WorkspaceViewModel.OpenFileCommand`) opens an unfiltered file picker; the chosen file opens (or focuses) a tab the same way.
- OS file association at startup (see §4.1).

In every case:
- `.llx` files open a `CnlTabViewModel` (editor + execution panel split).
- Any other file opens a `PlainTextTabViewModel` (generic text editor only).

Opening files is never gated by execution state — only *starting* an execution is (see §7).

### 4.3 Opening a Folder
File > Open Folder (`WorkspaceViewModel.OpenFolderCommand`) is a **new entry point to existing functionality** — the same command already reachable from the welcome/empty‑state action and the `Ctrl+K,Ctrl+O` shortcut. It does not introduce new behavior.

### 4.4 Creating an Untitled File
File > New LLX File and File > New TXT File (`WorkspaceViewModel.NewLlxFileCommand`/`NewTxtFileCommand`) create a new tab immediately, with no picker involved:
- A `CnlTabViewModel` or `PlainTextTabViewModel` respectively, with `IsUntitled = true`, `FilePath = null`, `IsDirty = false`.
- Titled `Untitled-N` from a single shared session‑scoped counter across both commands.
- Opened and focused immediately, with no folder required to be open.
- Not persisted to disk until Save or Save As (see `ui-viewmodels.md` §3).

---

# 5. Execution Behavior

Pipeline execution begins when the user clicks, within a `.llx` tab:

- **Run** (invokes `POST /trace`)
- **Explain** (invokes `POST /explain`)

### 5.1 On Execution Start
When that tab's `EditorViewModel` begins execution:

1. `IExecutionLockService.TryAcquire(tab)` succeeds (only possible if no other tab currently holds the lock).
2. That tab's `PipelineExecutionViewModel.IsRunning = true` (set on `pipeline_started`, per `ui-viewmodels.md` §7).
3. Run/Explain disable on **every** open tab, and the Settings gear disables, for the duration of the lock.
4. That tab's inspectors are cleared.
5. That tab's execution panel updates in place as streaming events arrive.

### 5.2 Streaming Events
The tab that started the execution remains the only one whose inspectors update. No workspace‑area change occurs during streaming — the user may freely switch to, open, or close other tabs while streaming continues in the background for the executing tab.

| Event Type | Effect |
|------------|--------|
| `pipeline_started` | Executing tab's inspectors clear; lock held |
| `raw_ast_generated` | Executing tab's Raw AST panel updates |
| `normalized_ast_generated` | Executing tab's Normalized AST panel updates |
| `ir_generated` | Executing tab's IR panel updates |
| `prompt_generated` | Executing tab's Prompt panel appends the new prompt (may fire multiple times per execution) |
| `model_output_generated` | Executing tab's Model Output panel appends the new output (may fire multiple times per execution) |
| `final_result_ready` | Executing tab's Final Result panel updates; lock released |
| `pipeline_failed` | Executing tab's error banner appears; lock released |

---

# 6. Error Rendering

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
- Errors render in the owning tab's own execution panel and error banner — they are never global across tabs.
- Errors must be surfaced immediately:
  - that tab's error banner  
  - that tab's inspector panels (if applicable)  
  - inline in that tab's editor (validation errors)
- Switching away from the tab that has an error does not clear it; the error persists on that tab until it re‑executes or the user dismisses its banner (see `ui-error-handling.md` §8).
- Closing a tab with unsaved changes or a visible error still goes through the standard unsaved‑changes confirmation if the tab is dirty; a visible error alone does not block closing a clean tab.

---

# 7. Execution Concurrency Constraints (App‑Wide Single Execution)

### 7.1 Forbidden Actions While an Execution Is In Flight
While `IExecutionLockService.IsAnyExecutionRunning == true`, the following are forbidden:

| Action | Allowed? |
|--------|----------|
| Starting Run/Explain in the executing tab | ❌ Forbidden (already running) |
| Starting Run/Explain in any other tab | ❌ Forbidden |
| Opening the Settings modal | ❌ Forbidden |

The following are explicitly **allowed** and unaffected by the lock:

| Action | Allowed? |
|--------|----------|
| Switching the active tab | ✔ Allowed |
| Opening a new tab from the Explorer | ✔ Allowed |
| Closing any tab (including the executing one — see note) | ✔ Allowed |
| Browsing/expanding the folder tree | ✔ Allowed |
| Opening the About modal | ✔ Allowed |

Note: closing the tab that owns the in‑flight execution ends that tab's interest in the results but does not necessarily need to abort the HTTP/WebSocket exchange at the protocol level; regardless, once the tab is closed, `IExecutionLockService` must still release the lock when the terminal event (`final_result_ready`/`pipeline_failed`) arrives, or immediately upon close — implementations must pick one and document it in `ui-viewmodels.md` §6, but the lock must never be left permanently held by a closed tab.

### 7.2 Allowed Actions After Execution Completes
When `final_result_ready` or `pipeline_failed` arrives for the executing tab:

- `IExecutionLockService.IsAnyExecutionRunning` becomes `false`.
- Run/Explain re‑enable on every tab.
- The Settings gear re‑enables.

### 7.3 Rationale
This constraint ensures:

- deterministic state  
- no partial inspector rendering  
- no orphaned streaming events  
- no mismatched correlation IDs  
- no UI inconsistencies  

while still allowing free navigation of the workspace (tabs, tree) during a long‑running execution, matching the VS Code‑style interaction model.

---

# 8. Settings Modal Scenarios

### 8.1 Opening Settings
**GIVEN** no execution is in flight  
**WHEN** the user clicks the gear icon  
**THEN** the Settings modal opens  
**AND** the previously active tab remains underneath, unaffected

### 8.2 Settings Blocked During Execution
**GIVEN** an execution is in flight in some tab  
**WHEN** the user clicks the gear icon  
**THEN** the Settings modal does not open (gear is disabled)

### 8.3 Closing Settings
**GIVEN** the Settings modal is open  
**WHEN** the user saves or cancels  
**THEN** the modal closes and the previously active tab is shown again  
**AND** if there are unsaved Settings changes and the user attempts to close without saving, the unsaved‑changes confirmation dialog appears (same dialog as tab‑close, see §6)

---

# 9. About Modal Scenarios

### 9.1 Opening About
**GIVEN** the application is running  
**WHEN** the user selects Help > About  
**THEN** the About modal opens, showing app name, description, version, and GitHub link  
**AND** the previously active tab remains underneath, unaffected

### 9.2 About Never Blocked During Execution
**GIVEN** an execution is in flight in some tab  
**WHEN** the user selects Help > About  
**THEN** the About modal opens normally — explicit contrast with §8.2, since About has no backend side effects

### 9.3 Closing About
**GIVEN** the About modal is open  
**WHEN** the user closes it  
**THEN** the modal closes and the previously active tab is shown again  
**AND** no unsaved‑changes confirmation is possible, since About has no editable state

---

# 10. First‑Run Setup Gating

If `config.json` is missing/invalid or `ANTHROPIC_API_KEY` is unset (first launch, or a broken config):

- The Explorer and Tab Strip remain fully usable — opening a folder, browsing the tree, and opening tabs require no backend and are never gated by first‑run status.
- Run/Explain remain disabled on every `.llx` tab, and the Settings modal auto‑opens, until the user saves valid Settings (see `ui-deployment.md` §4.4 Step 4).
- File menu items that require no backend (New LLX File, New TXT File, Open File, Open Folder, Save, Save As, Save All) and Help > About remain usable pre‑config, same as the Explorer and Tab Strip.

This replaces the old page‑level gate ("Home/Editor/Execution remain unreachable") with an action‑level gate limited to backend‑dependent operations.

---

# 11. WorkspaceViewModel Responsibilities

### Must:
- enforce deterministic tab/folder state  
- expose tab and folder commands  
- coordinate with `IExecutionLockService` for the Run/Explain/Settings gate  
- ensure the executing tab's panel reflects streaming state in real time  
- coordinate the About modal (never gated)  
- expose New/Open/Save file commands

### Must Not:
- depend on Views  
- depend on inspector state directly (that belongs to each tab's `PipelineExecutionViewModel`)  
- depend on HTTP or WebSocket services  
- perform pipeline logic  
- perform error handling logic

---

# 12. Non‑Goals

This model does **not** support:

- parallel pipeline executions (per‑tab concurrent execution is a possible future extension, see `ui-architecture.md` §12)  
- queued executions  
- cancellation  
- nondeterministic animations  
- implicit transitions  
- plugin panels  
- a persisted project/workspace manifest (open folder/tabs are session state, not a saved project file)  
- custom routing logic in Views
- a native OS menu bar  
- autosave  
- menus beyond File and Help

---

# 13. Future Extensions

Potential enhancements:

- queued execution mode  
- cancelable pipelines  
- animated transitions (if deterministic)  
- per‑tab independent execution (concurrent executions across tabs)  
- plugin panels

---

# Summary

Limelight‑X's workspace shell is deterministic, MVVM‑driven, and tightly integrated with the streaming pipeline model.  
Execution always happens in place inside the tab that started it, and streaming events update that tab's own execution panel until it completes or fails.  
An app‑wide single‑execution lock ensures predictable behavior and stable inspector rendering, while tab switching, opening, closing, and folder browsing remain free at all times.  
A MenuBar provides File and Help entry points for tab/file management and the About modal; About, unlike Settings, is never gated by the execution lock.
