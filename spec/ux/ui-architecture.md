# UI Architecture

## Purpose
This document defines the complete architecture of the Limelight‑X UI.  
It specifies the module boundaries, navigation model, data flow, and deterministic behavior of the Avalonia‑based workflow dashboard.  
This specification is authoritative.  
All implementation must follow this architecture exactly.

The UI is designed for analysts and citizen developers.  
It exposes the Limelight‑X pipeline through a clean, deterministic interface backed by an HTTP API that wraps the CLI commands (`run`, `explain`, `trace`).

---

# 1. High‑Level Overview

The Limelight‑X UI is a **multi‑page workflow dashboard** built using Avalonia and MVVM.  
It provides:

- a CNL editor with syntax highlighting, live validation, and auto‑formatting  
- a deterministic execution workflow  
- collapsible inspector panels for:
  - Raw AST  
  - Normalized AST  
  - IR  
  - Prompt viewer  
  - Model output viewer  
- a vertical pipeline timeline mirroring CLI output order  
- integration with a Limelight‑X HTTP API server that wraps CLI commands

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
   - Services handle HTTP communication.

3. **CLI‑Equivalent Backend**  
   - UI calls HTTP endpoints that wrap CLI commands:
     - `/run`
     - `/explain`
     - `/trace`
   - UI parses structured output into inspector ViewModels.

4. **Single Responsibility Modules**  
   - Each module has exactly one conceptual responsibility.  
   - No “utils” or “helpers” modules.

5. **Analyst‑Friendly Workflow**  
   - Vertical pipeline visualization.  
   - Clear separation between editing and inspection.  
   - Errors surfaced inline and in inspectors.

6. **Platform Targets**  
   - Windows (v0.1)  
   - macOS (future extension)

7. **Non‑Extensible v0.1**  
   - No plugin system.  
   - No custom inspectors.  
   - No multi‑file projects.

---

# 3. Module Structure

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

# 4. Navigation Model

The UI uses a **multi‑page workflow** with deterministic routing:

1. **Home Page**  
   - Open `.llx` file  
   - Recent files  
   - Quick actions

2. **Editor Page**  
   - CNL editor  
   - Live validation  
   - Syntax highlighting  
   - Auto‑formatting  
   - “Run”, “Explain”, “Trace” buttons

3. **Execution Page**  
   - Vertical pipeline timeline  
   - Collapsible inspector panels  
   - Final result viewer

Navigation is controlled by a `NavigationViewModel` and must be deterministic.

---

# 5. ViewModels

The UI defines the following ViewModels:

### 5.1 FileLoaderViewModel
- Opens `.llx` files  
- Tracks recent files  
- Validates file existence  
- Emits file content to EditorViewModel

### 5.2 EditorViewModel
- Holds CNL text  
- Performs live validation via `/explain`  
- Tracks syntax errors  
- Provides commands:
  - `RunCommand`
  - `ExplainCommand`
  - `TraceCommand`

### 5.3 PipelineExecutionViewModel
- Holds structured output from `/run`, `/explain`, `/trace`  
- Maps backend output into inspector ViewModels  
- Tracks execution status and errors

### 5.4 Inspector ViewModels
Each inspector has its own ViewModel:

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

---

# 6. HTTP API Integration

The UI communicates with the `/src/api` server defined in `spec/api.md` (started via `llx serve`), which wraps CLI commands.

### Endpoints

```
POST /run
POST /explain
POST /trace
```

### Request Body

```
{
  "source": "<CNL text>"
}
```

### Response Structure

Each endpoint returns structured JSON containing:

- raw_ast  
- normalized_ast  
- ir  
- prompts (trace only)  
- model_outputs (trace only)  
- final_result (run only)

### Rules

- UI must not call Rust directly.  
- UI must not reconstruct pipeline stages manually.  
- UI must parse structured output deterministically.  
- UI must surface backend errors immediately.

---

# 7. Inspector Panels

Inspector panels appear as **collapsible vertical sections** in the Execution Page:

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

### Rules

- Panels must be collapsible.  
- Panels must reflect ViewModel state.  
- Panels must not contain logic.  
- Panels must display structured output exactly as returned by the backend.

---

# 8. Editor Requirements

The editor must support:

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

---

# 9. Deterministic State Model

UI state must be fully derived from ViewModels.

### Allowed UI State
- panel collapse/expand  
- selected tab  
- selected file  
- editor cursor position

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

Errors must appear:

- inline in the editor  
- in the Execution Page  
- in inspector panels  
- in a global error banner

All errors must be human‑readable.

---

# 11. Non‑Goals

The UI does **not** support:

- plugins  
- custom inspectors  
- multi‑file projects  
- direct Rust integration  
- macOS (v0.1)  
- nondeterministic UI behavior  
- pipeline visualization beyond vertical timeline  
- reimplementing pipeline stages (parsing, normalization, IR compilation, evaluation) — the UI only sequences requests to `/run`, `/explain`, `/trace` and renders their responses; it does not perform any pipeline stage itself

---

# 12. Future Extensions

Potential future enhancements:

- macOS support  
- plugin system  
- visual IR graph  
- workflow templates  
- richer inspectors  
- multi‑file project support  
- integrated model output diffing

---

# Summary

The Limelight‑X UI is a deterministic workflow dashboard built using Avalonia and MVVM.  
It integrates with a CLI‑equivalent HTTP API, provides a CNL editor with live validation, and exposes the full pipeline through collapsible vertical inspector panels.  
All behavior is spec‑driven, deterministic, and aligned with the Limelight‑X architecture.