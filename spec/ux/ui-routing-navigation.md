# UI Routing & Navigation

## Purpose
This document defines the routing and navigation system for the Limelight‑X Avalonia workflow dashboard.  
It specifies page transitions, navigation guards, state persistence rules, and deterministic behavior.  
This specification is authoritative.  
All implementation must follow this routing model exactly.

Limelight‑X uses a **pure ViewModel‑driven routing system** with **no URL routing**, **no history stack**, and **instant transitions**.  
Navigation is deterministic, guard‑controlled, and aligned with the workflow: Home → Editor → Execution.

---

# 1. Routing Model

Limelight‑X uses **ViewModel‑driven routing only**.

### Rules
- No URL routing  
- No deep‑linking  
- No browser‑style history  
- No route parameters  
- No external navigation state  
- Pages are switched by setting `NavigationViewModel.CurrentPage`

### Page Types
```
PageType {
    Home,
    Editor,
    Execution
}
```

---

# 2. Navigation Flow

The workflow is strictly linear:

```
Home → Editor → Execution
```

### HomePage
- Entry point  
- User selects or opens a `.llx` file  
- Navigation guard: file must exist

### EditorPage
- User edits CNL  
- User triggers Run / Explain / Trace  
- Navigation guard: valid CNL required

### ExecutionPage
- Displays pipeline results  
- Navigation guard: backend must return a successful response

---

# 3. NavigationViewModel

### State
```
CurrentPage: PageType
```

### Commands
- `NavigateToHomeCommand`
- `NavigateToEditorCommand`
- `NavigateToExecutionCommand`

### Responsibilities
- Manage current page only  
- Enforce navigation guards  
- Trigger modal dialogs on navigation failure  
- Provide deterministic page transitions

### Forbidden
- No history stack  
- No forward/back navigation  
- No route parameters  
- No implicit transitions  

---

# 4. Navigation Guards

Navigation guards enforce deterministic workflow correctness.

### Guard 1: Editor requires a loaded file
```
Home → Editor allowed only if:
FileLoaderViewModel.FileContent != null
```

### Guard 2: Execution requires valid CNL
```
Editor → Execution allowed only if:
EditorViewModel.ValidationErrors.Count == 0
```

### Guard 3: Execution requires successful backend response
```
Editor → Execution allowed only if:
PipelineService.RunAsync/ExplainAsync/TraceAsync returns success
```

### Guard 4: No navigation allowed during execution
```
IsRunning == false
IsTracing == false
IsExplaining == false
```

### Guard Failure Behavior
Navigation failures trigger a **modal dialog**:

```
ModalDialog {
    Title: "Navigation Blocked"
    Message: <reason>
    Buttons: [OK]
}
```

---

# 5. Execution Routing Behavior

When the user triggers:

- Run  
- Explain  
- Trace  

Routing follows this deterministic sequence:

### Step 1 — Validate CNL
If validation fails → modal dialog → stay on EditorPage.

### Step 2 — Call backend
UI waits for backend response.

### Step 3 — If backend succeeds
Populate inspector ViewModels → navigate to ExecutionPage.

### Step 4 — If backend fails
Show modal dialog → stay on EditorPage.

### Rules
- No navigation until backend returns  
- No partial navigation  
- No “loading” ExecutionPage  
- No inline inspectors on EditorPage  

---

# 6. Inspector Routing

Inspector panels (Raw AST, Normalized AST, IR, Prompts, Model Outputs, Final Result) use:

### **Internal route IDs only**
```
execution/raw-ast
execution/normalized-ast
execution/ir
execution/prompts
execution/model-outputs
execution/final-result
```

### Rules
- Internal route IDs are **not exposed to the user**  
- No direct navigation to inspectors  
- Inspectors are collapsible sections inside ExecutionPage  
- Inspector collapse/expand state persists across navigation

---

# 7. State Persistence

Limelight‑X preserves state across pages:

### EditorPage
- CNL text persists  
- Cursor position persists  
- Selection persists  
- Undo/redo stack persists  
- Validation errors persist

### ExecutionPage
- Inspector collapse/expand states persist  
- Pipeline results persist until replaced  
- Errors persist until cleared

### HomePage
- Recent files persist  
- Last opened file path persists

---

# 8. Page Transition Behavior

### Transition Style
- **Instant**  
- No fade  
- No slide  
- No animation beyond inspector collapse/expand

### Rules
- Page transitions must be deterministic  
- No asynchronous transitions  
- No animation sequencing  
- No transition delays

---

# 9. Sidebar Navigation

Limelight‑X uses a **sidebar navigation layout**.

### Sidebar Items
- Home  
- Editor  
- Execution  

### Rules
- Sidebar reflects `CurrentPage`  
- Sidebar navigation respects guards  
- Sidebar cannot override guard failures  
- Sidebar cannot navigate to ExecutionPage unless pipeline succeeded  
- Sidebar cannot navigate to EditorPage unless a file is loaded

---

# 10. Navigation Error Handling

Navigation errors use **modal dialogs**.

### Modal Dialog Behavior
- Blocks navigation  
- Does not change page  
- Provides clear reason  
- Uses Limelight‑X styling (dark theme, neon accent sparingly)

### Error Types
- Missing file  
- Invalid CNL  
- Backend failure  
- Guard violation  
- Unexpected navigation state

---

# 11. Non‑Goals

Routing does **not** support:

- URL routing  
- Deep‑linking  
- History stack  
- Forward/back navigation  
- Multi‑file project navigation  
- Plugin pages  
- Inspector‑level navigation  
- Animated page transitions  

---

# 12. Future Extensions

Potential future routing enhancements:

- Deep‑linking for inspector panels  
- Multi‑file project routing  
- Plugin page routing  
- URL‑based navigation  
- History stack  
- Execution timeline navigation  

---

# Summary

Limelight‑X uses a deterministic, guard‑driven, ViewModel‑only routing system.  
Navigation is instant, linear, and strictly controlled: Home → Editor → Execution.  
ExecutionPage is only reachable after successful validation and backend execution.  
Inspector panels use internal route IDs but are not directly navigable.  
All navigation failures use modal dialogs, and all page state persists across transitions.  
This routing model is fixed for v0.1 and must be followed exactly.