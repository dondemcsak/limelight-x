# UI ViewModels

## Purpose
This document defines all ViewModels used in the Limelight‑X Avalonia workflow dashboard.  
It specifies the state model, commands, responsibilities, and deterministic behavior of each ViewModel.  
This specification is authoritative.  
All implementation must follow this ViewModel catalog exactly.

ViewModels must be:

- deterministic  
- MVVM‑pure  
- free of UI logic  
- free of business logic  
- consumers of HTTP API services  
- aligned with the Limelight‑X pipeline and inspector components  

---

# 1. Overview

Limelight‑X ViewModels fall into four categories:

1. **Structural ViewModels**  
   Navigation, file loading, page state.

2. **Editor ViewModels**  
   CNL editing, validation, auto‑completion, hover tooltips, quick‑fix suggestions, formatting.

3. **Pipeline Execution ViewModels**  
   Execution state, inspector ViewModels, pipeline results.

4. **Inspector ViewModels**  
   Raw AST, Normalized AST, IR, Prompts, Model Outputs, Final Result — each with custom rendering data.

All ViewModels use:

- **PascalCase naming**  
- **Avalonia Community Toolkit commands**  
- **multiple loading flags** (`IsRunning`, `IsValidating`, `IsTracing`)  
- **structured error lists** using a shared base type with domain‑specific subtypes  
- **parsed objects + raw strings + metadata** for AST and IR  

---

# 2. Error Model

All ViewModels use the shared base error type defined in `ui-error-handling.md` §1 (canonical — this section must match it exactly, not redefine it):

```
UiError {
    Code: string
    Message: string
    Severity: ErrorSeverity
    Category: ErrorCategory
    Location: ErrorLocation?
}
```

### Severity
```
ErrorSeverity {
    Info,
    Warning,
    Error,
    Fatal
}
```

### Category
```
ErrorCategory {
    Validation,
    Pipeline,
    Api,
    Rendering,
    Navigation,
    Editor,
    State
}
```

`Code` and `Category` are populated directly from the wire response's `code`/`category` fields (see `ui-data-contracts.md` §1, `api.md` §10) — the UI does not derive them itself. `Severity`/`Category` strings are deserialized case-insensitively from the lowercase wire values into these enums.

### Domain‑specific subtypes
Each domain may extend `UiError`:

- `ValidationError : UiError`
- `PipelineError : UiError`
- `ApiError : UiError`
- `RenderingError : UiError`

All ViewModels expose:

```
Errors: ObservableCollection<UiError>
```

Errors must be deterministic and human‑readable.

---

# 3. Structural ViewModels

## 3.1 NavigationViewModel
### Purpose
Controls deterministic navigation between pages.

### State
```
CurrentPage: PageType
```

### Commands
- `NavigateToHomeCommand`
- `NavigateToEditorCommand`
- `NavigateToExecutionCommand`
- `NavigateToSettingsCommand`

### Behavior
- No history stack  
- No deep‑linking  
- Deterministic page transitions  
- Enforces Guard 5 (unsaved Settings changes) before leaving SettingsPage, per `ui-routing-navigation.md` §4  

---

## 3.2 FileLoaderViewModel
### Purpose
Loads `.llx` files and provides file content to the editor.

### State
```
SelectedFilePath: string
RecentFiles: ObservableCollection<string>
FileContent: string
Errors: ObservableCollection<UiError>
IsLoading: bool
```

### Commands
- `OpenFileCommand`
- `LoadFileCommand`

### Behavior
- Reads file content  
- Validates file existence  
- Emits content to `EditorViewModel`  
- Adds file to recent list  

---

## 3.3 SettingsViewModel
### Purpose
Holds and validates the editable deployment configuration (`ui-deployment.md` §4.3), and applies changes by relaunching `llx serve`.

### State
```
Port: int
LogPath: string
ApiKey: string
EnvironmentProfile: EnvironmentProfile
IsDirty: bool
IsApplying: bool
Errors: ObservableCollection<UiError>
```

### EnvironmentProfile
```
EnvironmentProfile {
    Dev,
    Stage,
    Prod
}
```

### Commands
- `SaveSettingsCommand`
- `CancelSettingsCommand`
- `ToggleApiKeyVisibilityCommand`

### Persistence Model
- `Port`, `LogPath`, `EnvironmentProfile` are read from and written to a single JSON file at `%APPDATA%\LimelightX\config.json` (see `ui-deployment.md` §4.3 for the schema).  
- `ApiKey` is read from and written to Windows Credential Manager only — it is never written to `config.json`. There is a single shared `ApiKey`, independent of `EnvironmentProfile`; profiles only affect `Port`/`LogPath` defaults, not credential storage.

### Process Ownership
`LimelightX.exe` holds the `System.Diagnostics.Process` handle for the `llx.exe serve` process it launches at startup (`ui-deployment.md` §7). `SettingsViewModel` uses that same handle — it does not track the process by PID file or IPC.

### Behavior
- `Port` is a bind **port number only** — the bind host is fixed at `127.0.0.1` (see `SECURITY.md`, `api.md`) and is never user-editable.
- Editing any field sets `IsDirty = true`.
- `SaveSettingsCommand`:
  1. Validates all fields (see `ui-error-handling.md` §10); if invalid, populates `Errors` and does not proceed.
  2. Writes `Port`, `LogPath`, `EnvironmentProfile` to `config.json`.
  3. Writes `ApiKey` to Windows Credential Manager (never to `config.json`).
  4. Sets `IsApplying = true`. Requests graceful shutdown of the held `llx.exe serve` process handle (the same clean-shutdown path used on normal app exit, per `api.md` §8) and awaits its exit; then calls `Process.Start()` again with the new `--port` and `ApiKey` in the environment. If no process is currently running (e.g. the previous launch failed at startup, see `ui-deployment.md` §4.4), this step skips straight to `Process.Start()`.
  5. On success: `IsApplying = false`, `IsDirty = false`, navigates back to the previous page (or to HomePage if this was the first-launch flow, see `ui-routing-navigation.md` §2).
  6. On failure (e.g. new port unavailable, relaunch crash): `IsApplying = false`, raises a `Category: Api, Severity: fatal` error (see `ui-error-handling.md` §10) — `IsDirty` remains `true` so the user's edits are not lost.
- `CancelSettingsCommand`: disabled while `IsApplying == true`. Otherwise, if `IsDirty`, triggers Guard 5's confirmation modal (`ui-routing-navigation.md` §4); if not dirty, navigates back immediately.
- `ToggleApiKeyVisibilityCommand`: toggles masked/unmasked display of `ApiKey`; does not affect `IsDirty`.

### Deterministic Behavior
- No autosave — changes only take effect via explicit `SaveSettingsCommand`.  
- No retries on relaunch failure.  
- No partial apply — port/log path/profile and API key are written together or not at all.

---

# 4. Editor ViewModels

## 4.1 EditorViewModel
### Purpose
Primary ViewModel for CNL editing.

### State
```
Text: string
ValidationErrors: ObservableCollection<ValidationError>
CompletionItems: ObservableCollection<CompletionItem>
QuickFixes: ObservableCollection<QuickFixItem>
HoverInfo: HoverInfo?
CursorPosition: int
SelectionRange: (int Start, int End)
UndoStack: Stack<EditorAction>
RedoStack: Stack<EditorAction>
IsValidating: bool
IsFormatting: bool
IsCompleting: bool
Errors: ObservableCollection<UiError>
```

### Commands
- `RunCommand`
- `ExplainCommand`
- `TraceCommand`
- `FormatCommand`
- `ApplyQuickFixCommand`
- `SelectCompletionItemCommand`
- `UndoCommand`
- `RedoCommand`

### Responsibilities
- Manage editor text  
- Trigger validation via `/explain`  
- Provide auto‑completion  
- Provide hover tooltips  
- Provide quick‑fix suggestions  
- Manage cursor and selection  
- Manage undo/redo  
- Apply deterministic formatting  

### Deterministic Behavior
- Formatting must be deterministic  
- Validation must reflect backend output exactly  
- Completion items must be grammar‑driven  
- Quick‑fix suggestions must be rule‑based  
- Undo/redo must be deterministic  

---

# 5. Pipeline Execution ViewModels

## 5.1 PipelineExecutionViewModel
### Purpose
Coordinates pipeline execution and inspector ViewModels.

### State
```
RawAstViewModel: RawAstViewModel
NormalizedAstViewModel: NormalizedAstViewModel
IrViewModel: IrViewModel
PromptViewModel: PromptViewModel
ModelOutputViewModel: ModelOutputViewModel
FinalResultViewModel: FinalResultViewModel

IsRunning: bool
IsTracing: bool
IsExplaining: bool

Errors: ObservableCollection<UiError>
```

### Commands
- `RunPipelineCommand`
- `ExplainPipelineCommand`
- `TracePipelineCommand`

### Responsibilities
- Call `PipelineService`  
- Map structured backend output into inspector ViewModels  
- Track execution state  
- Surface pipeline errors  

### Deterministic Behavior
- Inspector ViewModels must be populated deterministically  
- Execution state must be explicit  
- No hidden transitions  

---

# 6. Inspector ViewModels

Each inspector ViewModel uses:

- parsed objects  
- raw string  
- metadata  
- collapse/expand state  
- error list  

---

## 6.1 RawAstViewModel
### State
```
Tree: AstNode
RawText: string
Metadata: AstMetadata
IsCollapsed: bool
Errors: ObservableCollection<UiError>
```

### Responsibilities
- Provide AST tree structure for modern tree view  
- Provide syntax‑highlighted raw text  
- Provide metadata (node depth, node count)  

---

## 6.2 NormalizedAstViewModel
### State
```
Tree: AstNode
RawText: string
Metadata: AstMetadata
IsCollapsed: bool
Errors: ObservableCollection<UiError>
```

### Responsibilities
Same as RawAstViewModel, but with normalized AST rules applied.

---

## 6.3 IrViewModel
### State
```
Operations: ObservableCollection<IrOperation>
RawText: string
Metadata: IrMetadata
IsCollapsed: bool
Errors: ObservableCollection<UiError>
```

### Responsibilities
- Provide IR operations for operation cards  
- Provide syntax‑highlighted raw IR  
- Provide metadata (operation count, reference map)  

---

## 6.4 PromptViewModel
### State
```
Prompts: ObservableCollection<PromptBlock>
RawText: string
Metadata: PromptMetadata
IsCollapsed: bool
Errors: ObservableCollection<UiError>
```

### Responsibilities
- Provide prompt blocks  
- Provide syntax‑highlighted Markdown  
- Provide metadata (operation index, prompt length)  

---

## 6.5 ModelOutputViewModel
### State
```
Outputs: ObservableCollection<ModelOutputBlock>
RawText: string
Metadata: ModelOutputMetadata
IsCollapsed: bool
Errors: ObservableCollection<UiError>
```

### Responsibilities
- Provide syntax‑highlighted Markdown  
- Provide JSON/table rendering metadata  
- Provide raw text for debugging  

---

## 6.6 FinalResultViewModel
### State
```
ResultText: string
ContentType: ResultContentType
RawText: string
IsCollapsed: bool
Errors: ObservableCollection<UiError>
```

### ContentType
```
ResultContentType {
    PlainText,
    Markdown,
    Json
}
```

### Responsibilities
- Provide syntax‑highlighted final result  
- Detect content type deterministically  
- Provide raw text for debugging  

---

# 7. Services

## 7.1 PipelineService
### Purpose
Wraps HTTP API calls to `/src/api` (started via `llx serve`, default `127.0.0.1:4747`). The wire format, endpoint paths, and error semantics are owned by `spec/api.md`; this service is a thin typed client over that contract.

### Methods
```
Task<ExplainResult> ExplainAsync(string source)
Task<RunResult> RunAsync(string source)
Task<TraceResult> TraceAsync(string source)
```

### Behavior
- Deterministic request/response handling  
- No UI logic  
- No caching  
- No retries  

---

# 8. Deterministic Behavior Requirements

All ViewModels must follow these rules:

1. **No nondeterministic state transitions**  
2. **No hidden state machines**  
3. **All state must be explicit**  
4. **All errors must be surfaced deterministically**  
5. **All inspector ViewModels must reflect backend output exactly**  
6. **EditorViewModel must be grammar‑driven**  
7. **Undo/redo must be deterministic**  
8. **Loading flags must be explicit (`IsRunning`, `IsValidating`, `IsTracing`)**  

---

# 9. Non‑Goals

ViewModels do **not** support:

- plugins  
- multi‑file projects  
- direct Rust integration  
- nondeterministic animations  
- pipeline orchestration  
- macOS‑specific behavior (v0.1)  

---

# 10. Future Extensions

Potential future ViewModels:

- AST semantic inspector  
- IR graph ViewModel  
- Prompt diff ViewModel  
- Multi‑file project ViewModel  
- macOS‑specific ViewModels  
- Plugin‑based inspector ViewModels  

---

# Summary

This document defines all ViewModels used in the Limelight‑X workflow dashboard.  
ViewModels are deterministic, MVVM‑pure, and aligned with the Limelight‑X pipeline and HTTP API.  
Inspector ViewModels expose parsed objects, raw text, and metadata for custom rendering components.  
All behavior is spec‑driven and must follow this specification exactly.
