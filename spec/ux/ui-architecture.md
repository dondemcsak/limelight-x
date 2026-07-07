# UI Architecture (Streaming Edition)

## Purpose
This document defines the complete architecture of the Limelight‑X UI.  
It specifies the module boundaries, workspace shell model, data flow, and deterministic behavior of the Avalonia‑based folder/tab workspace.  
This specification is authoritative.  
All implementation must follow this architecture exactly.

The UI is designed for analysts and citizen developers.  
It exposes the Limelight‑X pipeline through a clean, deterministic interface backed by an HTTP + WebSocket API that wraps the CLI commands (`run`, `explain`, `trace`) and streams pipeline results as JSON events.

---

# 1. High‑Level Overview

The Limelight‑X UI is a **folder‑explorer, tab‑based workspace** built using Avalonia and MVVM, in the style of a code editor: a folder directory tree on the left, and a tabbed panel on the right where opening a file from the tree opens (or focuses) a tab.  
It provides:

- a folder tree (Explorer) of a user‑selected root folder  
- a tabbed document area supporting multiple simultaneously open files  
- for `.llx` files: a CNL editor with syntax highlighting, live validation, and auto‑formatting, paired with an execution panel in the same tab  
- for plain text files: a generic text editor, no CNL semantics  
- exactly two execution triggers per `.llx` tab — **Run** (full pipeline trace) and **Explain** (AST only)  
- collapsible inspector panels for:
  - Raw AST  
  - Normalized AST  
  - IR  
  - Prompt viewer  
  - Model output viewer  
- a vertical pipeline timeline mirroring CLI output order, scoped to each tab  
- **real‑time integration with a streaming HTTP + WebSocket API** that emits pipeline events incrementally
- an app‑wide execution lock: only one pipeline execution may be in flight at a time, across all tabs

The UI is **deterministic**, **spec‑driven**, and **state‑derived**.  
All nondeterminism is isolated to model output returned by the backend.

---

# 2. Architectural Principles

1. **Deterministic UI State**  
   - All UI state is derived from ViewModels.  
   - No hidden transitions.  
   - No nondeterministic UI behavior.

2. **MVVM Purity**  
   - Views contain no logic.  
   - ViewModels contain state and commands.  
   - Services handle HTTP + WebSocket communication.  
   - Services handle persistent diagnostic logging via `Microsoft.Extensions.Logging`'s `ILogger`/`ILoggerFactory` abstractions, configured at the composition root with Serilog's file sink as the provider (see `ui-deployment.md` §4.3).

3. **CLI‑Equivalent Backend**  
   - UI calls the streaming API endpoints:
     - `POST /run`
     - `POST /explain`
     - `POST /trace`
   - The **Run** button invokes `POST /trace` (full pipeline detail); the **Explain** button invokes `POST /explain` (AST only). `POST /run` remains a valid backend endpoint but is not wired to any UI control in this shell — no button, shortcut, or menu item calls it.
   - UI receives pipeline results as **incremental JSON events** over WebSocket.  
   - UI maps each event into inspector ViewModels, scoped to the tab that started the execution.

4. **Single Responsibility Modules**  
   - Each module has exactly one conceptual responsibility.  
   - No “utils” or “helpers” modules.

5. **Analyst‑Friendly Workflow**  
   - Vertical pipeline visualization, scoped per tab.  
   - Clear separation between editing and inspection.  
   - Errors surfaced inline and in inspectors.  
   - Real‑time inspector updates as events arrive.

6. **Platform Targets**  
   - Windows (v0.1)  
   - macOS (future extension)

7. **Non‑Extensible v0.1**  
   - No plugin system.  
   - No custom inspectors.  
   - Multiple files may be open across tabs (see §4); this is not a "multi‑file project" system in the sense of a saved workspace/project manifest — there is no project file, no per‑project settings, and no cross‑file analysis.

---

# 3. Module Structure

### Project & Executable Naming
- **Project name (assembly name):** `LimelightX.UI`  
- **Executable:** `LimelightX.exe`  

The UI repository must follow this structure:

```
/ui
    /views
    /viewmodels
    /services
    /components
    /styles
    /routing
```

### Rules

- Each module has exactly one responsibility.  
- No circular dependencies.  
- ViewModels must not reference Views.  
- Services must not reference Views or ViewModels.  
- Styles must not contain logic.

---

# 4. Workspace Shell Model

The UI uses a **folder‑explorer + tab‑strip workspace**, replacing the previous page‑based navigation model entirely:

1. **MenuBar (title‑bar row)**  
   - Custom in‑window Avalonia `Menu`, styled with existing theme tokens (no native OS menu bar; see `ui-styling-theming.md` §4.9)  
   - Exactly two top‑level menus: **File** (New LLX File, New TXT File, Open File, Open Folder, Save, Save As, Save All, Settings) and **Help** (About)  
   - File > Settings routes to the same `OpenSettingsCommand`/`IsSettingsOpen` flow as the persistent gear icon (item 4 below) — the gear icon is not removed  
   - Help > About opens the About modal (item 4 below); About has no persistent icon of its own, the menu is its only entry point

2. **Explorer (left pane)**  
   - Folder directory tree of the currently open root folder  
   - "Open Folder" action  
   - Expand/collapse folders  
   - Clicking a file opens (or focuses, if already open) a tab in the Tab Content Area

3. **Tab Strip + Tab Content Area (right pane)**  
   - One tab per open file  
   - When no folder is open, or no tabs are open, shows a welcome/empty state with an "Open Folder" action (this replaces the old Home Page as a destination)  
   - `.llx` tab content: CNL editor (top) + execution panel (bottom), with **Run** and **Explain** buttons above the editor  
   - Plain text tab content: generic text editor only, no execution split  
   - Tabs are freely switchable, closable (with an unsaved‑changes confirmation for dirty tabs), and reorderable  
   - Tabs created via File > New LLX File / New TXT File are **untitled** — `IsUntitled = true`, no backing `FilePath`, titled `Untitled-N` (shared counter across both file kinds, e.g. `Untitled-1`, `Untitled-2`) — and are not written to disk until Save or Save As (see `ui-viewmodels.md` §3, §5.1)

4. **Modals — Settings and About**  
   - **Settings**: opened via a persistent gear icon (title/activity‑bar style) *or* File > Settings — both route to the same command; not a tab. Backend port, log path, `ANTHROPIC_API_KEY`, environment profile. Disabled while any execution is running app‑wide (see §9).  
   - **About**: opened only via Help > About; not a tab. Shows the app name, project description, version string, and a link to the GitHub repository. Unlike Settings, About has **no execution‑lock gating** — it has no backend side effects and remains available while an execution is in flight.

There is no more full‑page "Editor Page" / "Execution Page" / "Home Page" — editing and execution live together inside each `.llx` tab, and the Explorer replaces the old navigation Sidebar. The shell is controlled by a `WorkspaceViewModel` and must be deterministic (see `ui-viewmodels.md` §3–§5, `ui-routing-navigation.md`).

---

# 5. ViewModels

### 5.1 WorkspaceViewModel
- Holds the open root folder path  
- Owns the `FileTreeViewModel`  
- Owns the collection of open tabs and the active tab  
- Opens/focuses a tab when a file is selected in the tree

### 5.2 FileTreeViewModel
- Recursively scans the open root folder (client‑side filesystem only, no backend call)  
- Tracks expand/collapse state per node

### 5.3 TabViewModel family
- `TabViewModel` (base): header, file path, dirty state, close command  
- `CnlTabViewModel`: owns one `EditorViewModel` instance and one `PipelineExecutionViewModel` instance (per tab, not shared)  
- `PlainTextTabViewModel`: owns one `PlainTextEditorViewModel` instance (text only, no validation, no pipeline)

### 5.4 EditorViewModel (per `.llx` tab)
- Holds CNL text  
- Performs live validation via `/explain`  
- Tracks syntax errors  
- Provides commands:
  - `RunCommand` (invokes `/trace`)
  - `ExplainCommand` (invokes `/explain`)

### 5.5 PipelineExecutionViewModel (per `.llx` tab)
- Holds structured output from streaming events for that tab  
- Maps backend events into that tab's inspector ViewModels  
- Tracks this tab's own execution status and errors  
- Clears state when a new pipeline begins in this tab  
- Updates inspectors incrementally as events arrive

### 5.6 IExecutionLockService
- App‑wide: tracks whether any tab currently has an execution in flight  
- Gates every tab's Run/Explain commands and the Settings gear icon  
- Does **not** gate tab switching, tab open/close, or folder‑tree browsing

### 5.7 SettingsViewModel
- Holds backend port, log path, API key, environment profile  
- Validates input  
- Relaunches `llx serve` when settings change

### 5.8 Inspector ViewModels
Each inspector has its own ViewModel, instantiated per `.llx` tab:

- `RawAstViewModel`  
- `NormalizedAstViewModel`  
- `IrViewModel`  
- `PromptViewModel`  
- `ModelOutputViewModel`

Each ViewModel exposes:

- deterministic state  
- collapse/expand UI state  
- error state  
- formatted display text  
- incremental update methods for streaming events

---

# 6. HTTP + WebSocket API Integration

The UI communicates with the `/src/api` server defined in `spec/api.md`.

### Endpoints

```
POST /run
POST /explain
POST /trace
ws://127.0.0.1:<port>/events
```

### Request Body

```
{
  "source": "<CNL text>"
}
```

### Response Model

- HTTP response returns immediately with `{ accepted: true, correlation_id: "<id>" }`.
- All pipeline results are streamed as JSON events over WebSocket.
- Each event includes:
  - `event_type`
  - `correlation_id`
  - `version`
  - `success`
  - `errors`
  - `data`

### Rules

- UI must not call Rust directly.  
- UI must not reconstruct pipeline stages manually.  
- UI must update inspector ViewModels incrementally as events arrive, scoped to the tab that owns the active `correlation_id`.  
- UI must surface backend errors immediately.  
- UI must maintain deterministic ordering of events.  
- Only one execution may be in flight app‑wide at a time (see §9 and `IExecutionLockService`, §5.6).

---

# 7. Inspector Panels

Inspector panels appear as **collapsible vertical sections** in the bottom half of a `.llx` tab:

```
Raw AST
↓
Normalized AST
↓
IR
↓
Prompts
↓
Model Outputs
↓
Final Result
```

### Streaming Behavior

- Panels update **incrementally** as events arrive.  
- Raw AST panel appears when `raw_ast_generated` arrives.  
- Normalized AST panel appears when `normalized_ast_generated` arrives.  
- IR panel appears when `ir_generated` arrives.  
- Prompt and Model Output panels appear when their events arrive.  
- Final Result panel appears when `final_result_ready` arrives.
- Since Run now invokes `/trace`, all six panels are reachable from Run; Explain only ever populates Raw AST and Normalized AST (see `ui-data-contracts.md` §2).

### Rules

- Panels must be collapsible.  
- Panels must reflect ViewModel state.  
- Panels must display structured output exactly as streamed by the backend.

---

# 8. Editor Requirements

The `.llx` editor (CNL editor) must support:

### 8.1 Syntax Highlighting
- CNL keywords  
- resources  
- pronouns  
- expression holes  
- quoted strings

### 8.2 Live Validation
- Inline parser errors  
- Inline grammar errors  
- Inline expression hole errors  
- Errors retrieved via `/explain`

### 8.3 Auto‑Formatting
- Normalize whitespace  
- Ensure trailing period  
- Align expression hole indentation  
- Apply consistent spacing rules

### 8.4 Deterministic Behavior
- Formatting must be deterministic.  
- Validation must be deterministic.  
- No nondeterministic UI behavior.

Plain text tabs use a separate, generic text editor with none of the above CNL‑specific behavior (see `ui-components.md` §4).

---

# 9. Deterministic State Model

UI state must be fully derived from ViewModels.

### Allowed UI State
- panel collapse/expand  
- open tabs and tab order  
- active tab  
- expanded/collapsed folder tree nodes  
- editor cursor position  
- whether any execution is in flight app‑wide (`IExecutionLockService.IsAnyExecutionRunning`)  
- whether the Settings modal is open (`IsSettingsOpen`)  
- whether the About modal is open (`IsAboutOpen`)  
- per‑tab untitled state (`IsUntitled`, `FilePath`)

### Forbidden UI State
- implicit transitions  
- nondeterministic animations  
- hidden state machines  
- state not represented in ViewModels

---

# 10. Error Handling

The UI must surface:

- parser errors  
- normalizer errors  
- IR compiler errors  
- evaluator errors  
- model adapter errors  
- HTTP errors  
- malformed backend responses  
- **streaming pipeline errors (`pipeline_failed`)**

Errors must appear:

- inline in the editor  
- in that tab's execution panel  
- in inspector panels  
- in that tab's error banner

All errors must be human‑readable.

Every error surfaced this way is also persisted to the log file described in `ui-deployment.md` §4.3 (see `ui-error-handling.md` for the full logging contract and the failure‑safety rule).

---

# 11. Non‑Goals

The UI does **not** support:

- plugins  
- custom inspectors  
- direct Rust integration  
- macOS (v0.1)  
- nondeterministic UI behavior  
- pipeline visualization beyond vertical timeline  
- reimplementing pipeline stages  
- **single‑response API mode** (removed)
- a saved project/workspace manifest (open tabs and folder are session state only, not persisted as a project file)
- a native OS menu bar (the MenuBar is a custom in‑window control, see §4)
- menus beyond File and Help (no Edit, View, or other top‑level menus)
- autosave (untitled and dirty tabs are only persisted via an explicit Save/Save As/Save All)

---

# 12. Future Extensions

Potential future enhancements:

- macOS support  
- plugin system  
- visual IR graph  
- workflow templates  
- richer inspectors  
- integrated model output diffing  
- additional streaming observability events (timing, resource usage)
- per‑tab independent execution (concurrent executions across tabs), superseding the app‑wide single‑execution lock
- additional top‑level menus (Edit, View, etc.)

---

# Summary

The Limelight‑X UI is a deterministic folder‑explorer and tab‑based workspace built using Avalonia and MVVM.  
It integrates with a streaming HTTP + WebSocket API, provides a CNL editor with live validation inside each `.llx` tab, and exposes the full pipeline through collapsible vertical inspector panels that update in real time as events arrive, scoped to the tab that started the execution.  
A custom themed MenuBar (File, Help) provides discoverable entry points for file operations, untitled‑tab creation, Settings, and the About modal.  
All behavior is spec‑driven, deterministic, and aligned with the Limelight‑X architecture.
