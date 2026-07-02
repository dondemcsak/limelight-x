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
    Execution,
    Settings
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

### SettingsPage
- Not part of the linear Home → Editor → Execution workflow  
- Reachable from any page via the sidebar, or from HomePage's gear icon  
- Navigation guard: none to *enter*; leaving with unsaved changes requires confirmation (see Guard 5)  
- Subject to Guard 4 (no navigation during execution) like every other page

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
- `NavigateToSettingsCommand`

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

This applies to navigation into or out of any page, including SettingsPage — no new rule is needed for Settings specifically.

### Guard 5: Unsaved Settings changes require confirmation
```
Leaving SettingsPage (via sidebar, gear icon, or any NavigationViewModel command)
allowed only if:
SettingsViewModel.IsDirty == false
```
If `IsDirty == true`, navigation is blocked and a confirmation modal is shown instead of the standard guard-failure modal (see below).

### Guard Failure Behavior
Guards 1–4 failures trigger a **modal dialog**:

```
ModalDialog {
    Title: "Navigation Blocked"
    Message: <reason>
    Buttons: [OK]
}
```

Guard 5 failures trigger a distinct **confirmation modal** instead, since the user has a real choice rather than a blocked action:

```
ModalDialog {
    Title: "Unsaved Changes"
    Message: "You have unsaved settings changes. Discard them?"
    Buttons: [Stay, Discard Changes]
}
```
- **Stay** — remains on SettingsPage, `IsDirty` unchanged.  
- **Discard Changes** — reverts `SettingsViewModel` fields to last-saved values, sets `IsDirty = false`, and completes the original navigation.

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

### SettingsPage
- Saved values persist across app restarts (written to the config file / Credential Manager, see `ui-deployment.md` §4.3)  
- Unsaved edits do **not** persist across navigation away without confirmation (see Guard 5) — they are either saved or discarded, never silently carried

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
- Settings  

### Rules
- Sidebar reflects `CurrentPage`  
- Sidebar navigation respects guards  
- Sidebar cannot override guard failures  
- Sidebar cannot navigate to ExecutionPage unless pipeline succeeded  
- Sidebar cannot navigate to EditorPage unless a file is loaded  
- Sidebar's Settings item is reachable from any page, subject to Guard 4 (no navigation during execution) and Guard 5 (unsaved Settings changes)  
- Sidebar's Settings item uses the same `role="link"` / `aria-current="page"` pattern as Home, Editor, and Execution (see `ui-accessibility.md` §12)

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