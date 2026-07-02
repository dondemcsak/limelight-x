# BDD UI Interactions

## Purpose
This document defines Behavior‑Driven Development (BDD) scenarios for Limelight‑X.  
It specifies deterministic Given/When/Then interactions for all major workflows, inspector behaviors, navigation outcomes, error handling, and state persistence.  
Scenarios use **mock backend responses**, **medium granularity**, and **behavioral naming**.  
The document is organized by **workflow**.

Limelight‑X workflows covered:
- Load File  
- Edit  
- Run  
- Explain  
- Trace  
- Settings  

Each workflow includes:
- Successful interactions  
- Blocked interactions  
- Fatal interactions  
- Inspector interactions  
- State persistence behaviors  

All scenarios follow **pure Given/When/Then** format.

---

# 1. Load File Workflow

## Scenario: Loading a valid file opens the Editor
**Given** the user is on HomePage  
**And** a valid `.llx` file exists  
**When** the user selects the file  
**Then** the UI loads the file content  
**And** navigates to EditorPage

## Scenario: Loading an invalid file shows an error
**Given** the user is on HomePage  
**And** an invalid `.llx` file exists  
**When** the user selects the file  
**Then** the UI shows a global error banner  
**And** remains on HomePage

## Scenario: Loading a file persists editor state
**Given** the user previously edited a file  
**And** the editor contains unsaved text  
**When** the user loads the same file again  
**Then** the editor restores the previous text  
**And** restores validation state

---

# 2. Edit Workflow

## Scenario: Editing text updates validation state
**Given** the user is on EditorPage  
**And** the editor contains valid CNL  
**When** the user types invalid syntax  
**Then** validation errors appear inline  
**And** Run/Explain/Trace are disabled

## Scenario: Editing text clears previous pipeline results
**Given** the user previously ran a pipeline  
**And** ExecutionPage contains inspector results  
**When** the user edits the CNL  
**Then** inspector results are cleared  
**And** pipeline actions require re‑execution

## Scenario: Editor state persists across navigation
**Given** the user is on EditorPage  
**And** the editor contains text  
**When** the user navigates to HomePage  
**And** returns to EditorPage  
**Then** the editor text remains unchanged

---

# 3. Run Workflow

## Scenario: Running a valid pipeline produces a final result
**Given** the editor contains valid CNL  
**And** the backend mock response includes a final result  
**When** the user presses Ctrl+R  
**Then** the UI sends a Run request  
**And** navigates to ExecutionPage  
**And** displays the final result inspector

## Scenario: Running with validation errors is blocked
**Given** the editor contains invalid CNL  
**When** the user presses Ctrl+R  
**Then** the UI shows inline validation errors  
**And** does not send a backend request  
**And** remains on EditorPage

## Scenario: Running with backend failure shows inspector errors
**Given** the editor contains valid CNL  
**And** the backend mock response indicates failure  
**When** the user presses Ctrl+R  
**Then** the UI navigates to ExecutionPage  
**And** displays inline inspector errors  
**And** shows a global error banner

## Scenario: Fatal backend error blocks interaction
**Given** the editor contains valid CNL  
**And** the backend mock response indicates a fatal error  
**When** the user presses Ctrl+R  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 4. Explain Workflow

## Scenario: Explain produces raw and normalized AST
**Given** the editor contains valid CNL  
**And** the backend mock response includes raw and normalized AST  
**When** the user presses Ctrl+E  
**Then** the UI navigates to ExecutionPage  
**And** displays the Raw AST inspector  
**And** displays the Normalized AST inspector

## Scenario: Explain with invalid CNL is blocked
**Given** the editor contains invalid CNL  
**When** the user presses Ctrl+E  
**Then** inline validation errors appear  
**And** the UI remains on EditorPage

## Scenario: Explain with backend failure shows inspector errors
**Given** the editor contains valid CNL  
**And** the backend mock response indicates AST failure  
**When** the user presses Ctrl+E  
**Then** the UI navigates to ExecutionPage  
**And** displays inline AST errors  
**And** shows a global error banner

---

# 5. Trace Workflow

## Scenario: Trace produces IR, prompts, and model outputs
**Given** the editor contains valid CNL  
**And** the backend mock response includes IR, prompts, and model outputs  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** displays the IR inspector  
**And** displays the Prompts inspector  
**And** displays the Model Outputs inspector

## Scenario: Trace with invalid CNL is blocked
**Given** the editor contains invalid CNL  
**When** the user presses Ctrl+T  
**Then** inline validation errors appear  
**And** the UI remains on EditorPage

## Scenario: Trace with backend failure shows inspector errors
**Given** the editor contains valid CNL  
**And** the backend mock response indicates IR failure  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** displays inline IR errors  
**And** shows a global error banner

---

# 6. Settings Workflow

## Scenario: Editing a Settings field marks it dirty
**Given** the user is on SettingsPage  
**When** the user edits the Log Path field  
**Then** `SettingsViewModel.IsDirty` becomes `true`

## Scenario: Saving valid settings applies them and navigates back
**Given** the user is on SettingsPage  
**And** all fields contain valid values  
**And** the mocked `llx serve` relaunch succeeds  
**When** the user selects Save  
**Then** the UI shows an "Applying settings…" loading state  
**And** navigates back to the previous page  
**And** `IsDirty` becomes `false`

## Scenario: Toggling API key visibility unmasks the field
**Given** the user is on SettingsPage  
**And** the API key field is masked  
**When** the user selects the show/hide toggle  
**Then** the API key field displays its unmasked value

## Scenario: Canceling without changes returns immediately
**Given** the user is on SettingsPage  
**And** no fields have been edited  
**When** the user selects Cancel  
**Then** the UI navigates back to the previous page without a confirmation prompt

---

# 7. Inspector Interactions

## Scenario: Expanding an inspector reveals content
**Given** the user is on ExecutionPage  
**And** the IR inspector is collapsed  
**When** the user expands the IR inspector  
**Then** the IR tree becomes visible

## Scenario: Collapsing an inspector hides content
**Given** the IR inspector is expanded  
**When** the user collapses the IR inspector  
**Then** the IR tree becomes hidden

## Scenario: Inspector selection highlights a node
**Given** the Raw AST inspector is expanded  
**When** the user selects an AST node  
**Then** the node becomes highlighted  
**And** metadata for the node is displayed

## Scenario: Keyboard navigation moves inspector selection
**Given** an inspector is expanded  
**And** a node is selected  
**When** the user presses the down arrow  
**Then** the next node becomes selected

---

# 8. Navigation Scenarios

## Scenario: Sidebar navigation to EditorPage succeeds
**Given** the user is on HomePage  
**And** a file is loaded  
**When** the user selects Editor in the sidebar  
**Then** the UI navigates to EditorPage

## Scenario: Sidebar navigation to ExecutionPage is blocked
**Given** the user is on EditorPage  
**And** no pipeline has been executed  
**When** the user selects Execution in the sidebar  
**Then** the UI shows a modal dialog  
**And** remains on EditorPage

## Scenario: Fatal navigation error disables interaction
**Given** the user is on EditorPage  
**And** a fatal navigation error occurs  
**When** the user attempts to navigate  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 9. State Persistence

## Scenario: Editor text persists across navigation
**Given** the user enters text in the editor  
**When** the user navigates to HomePage  
**And** returns to EditorPage  
**Then** the editor text remains unchanged

## Scenario: Inspector collapse state persists
**Given** the user expands the IR inspector  
**When** the user navigates away from ExecutionPage  
**And** returns  
**Then** the IR inspector remains expanded

---

# Summary

This BDD specification defines deterministic Given/When/Then interactions for all major Limelight‑X workflows.  
It covers core workflows, inspector interactions, error handling, navigation outcomes, and state persistence.  
Backend responses are mocked, scenarios use medium granularity, and naming is behavioral.  
This interaction model is authoritative and must be followed exactly.
