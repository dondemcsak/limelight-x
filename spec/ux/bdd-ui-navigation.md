# BDD UI Navigation

## Purpose
This document defines Behavior‑Driven Development (BDD) scenarios for **navigation behavior** in Limelight‑X.  
It covers page transitions, sidebar navigation, navigation guards, modal blocking, inspector constraints, and navigation‑specific error handling.  
Scenarios use **mock backend responses**, **medium granularity**, and **behavioral naming**.  
The document is organized by **navigation type**.

All scenarios follow **pure Given/When/Then** format.

---

# 1. Sidebar Navigation

## Scenario: Navigating to EditorPage requires a loaded file
**Given** the user is on HomePage  
**And** no file is loaded  
**When** the user selects “Editor” in the sidebar  
**Then** the UI shows a navigation guard modal  
**And** remains on HomePage

## Scenario: Sidebar navigation to EditorPage succeeds when a file is loaded
**Given** the user is on HomePage  
**And** a file is loaded  
**When** the user selects “Editor” in the sidebar  
**Then** the UI navigates to EditorPage

## Scenario: Sidebar navigation to ExecutionPage requires a completed pipeline
**Given** the user is on EditorPage  
**And** no pipeline has been executed  
**When** the user selects “Execution” in the sidebar  
**Then** the UI shows a navigation guard modal  
**And** remains on EditorPage

## Scenario: Sidebar navigation to ExecutionPage succeeds after pipeline execution
**Given** the user is on EditorPage  
**And** the user previously executed a pipeline  
**When** the user selects “Execution” in the sidebar  
**Then** the UI navigates to ExecutionPage

## Scenario: Sidebar navigation preserves inspector collapse state
**Given** the user is on ExecutionPage  
**And** the IR inspector is expanded  
**When** the user navigates to EditorPage  
**And** returns to ExecutionPage  
**Then** the IR inspector remains expanded

## Scenario: Sidebar navigation to SettingsPage succeeds from any page
**Given** the user is on HomePage, EditorPage, or ExecutionPage  
**When** the user selects "Settings" in the sidebar  
**Then** the UI navigates to SettingsPage  
**And** fields reflect the last-saved configuration values

## Scenario: HomePage gear icon opens SettingsPage
**Given** the user is on HomePage  
**When** the user selects the gear icon  
**Then** the UI navigates to SettingsPage

---

# 2. Workflow Navigation

## Scenario: Running a pipeline navigates to ExecutionPage
**Given** the editor contains valid CNL  
**And** the backend mock response indicates success  
**When** the user presses Ctrl+R  
**Then** the UI navigates to ExecutionPage  
**And** displays the final result inspector

## Scenario: Explain navigates to ExecutionPage with AST inspectors
**Given** the editor contains valid CNL  
**And** the backend mock response includes raw and normalized AST  
**When** the user presses Ctrl+E  
**Then** the UI navigates to ExecutionPage  
**And** displays the Raw AST and Normalized AST inspectors

## Scenario: Trace navigates to ExecutionPage with IR and prompt inspectors
**Given** the editor contains valid CNL  
**And** the backend mock response includes IR, prompts, and model outputs  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** displays the IR, Prompts, and Model Outputs inspectors

---

# 3. Navigation Guards

## Scenario: Validation errors block navigation to ExecutionPage
**Given** the editor contains invalid CNL  
**When** the user presses Ctrl+R  
**Then** the UI shows inline validation errors  
**And** remains on EditorPage

## Scenario: Pipeline errors allow navigation but show inspector errors
**Given** the editor contains valid CNL  
**And** the backend mock response indicates pipeline failure  
**When** the user presses Ctrl+R  
**Then** the UI navigates to ExecutionPage  
**And** displays inline inspector errors  
**And** shows a global error banner

## Scenario: Navigation to ExecutionPage is blocked when backend returns no data
**Given** the editor contains valid CNL  
**And** the backend mock response contains no inspectors  
**When** the user presses Ctrl+T  
**Then** the UI shows a navigation guard modal  
**And** remains on EditorPage

## Scenario: Leaving SettingsPage with unsaved changes shows a confirmation modal
**Given** the user is on SettingsPage  
**And** the user has edited a field without saving  
**When** the user selects a different page in the sidebar  
**Then** the UI shows an "Unsaved Changes" confirmation modal  
**And** remains on SettingsPage

## Scenario: Choosing "Stay" keeps unsaved Settings changes
**Given** the "Unsaved Changes" confirmation modal is visible  
**When** the user selects "Stay"  
**Then** the modal closes  
**And** the UI remains on SettingsPage  
**And** the edited field values are unchanged

## Scenario: Choosing "Discard Changes" navigates away and reverts fields
**Given** the "Unsaved Changes" confirmation modal is visible  
**When** the user selects "Discard Changes"  
**Then** the UI navigates to the originally requested page  
**And** SettingsPage fields revert to their last-saved values on next visit

## Scenario: Navigation to SettingsPage is blocked during pipeline execution
**Given** the user is on EditorPage  
**And** a pipeline is currently running  
**When** the user selects "Settings" in the sidebar  
**Then** the UI shows a navigation guard modal  
**And** remains on EditorPage

---

# 4. Fatal Navigation Errors

## Scenario: Fatal navigation error disables interaction
**Given** the user is on EditorPage  
**And** a fatal navigation error occurs  
**When** the user attempts to navigate  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

## Scenario: Fatal backend error blocks navigation to ExecutionPage
**Given** the editor contains valid CNL  
**And** the backend mock response indicates a fatal error  
**When** the user presses Ctrl+R  
**Then** a modal dialog appears  
**And** the UI does not navigate  
**And** all actions are disabled until acknowledged

---

# 5. Inspector Navigation Constraints

## Scenario: Inspectors cannot be navigated to directly
**Given** the user is on EditorPage  
**When** the user attempts to navigate directly to an inspector  
**Then** the UI shows a navigation guard modal  
**And** remains on EditorPage

## Scenario: Inspectors are only visible on ExecutionPage
**Given** the user is on HomePage  
**When** the user attempts to open the IR inspector  
**Then** the UI shows a navigation guard modal  
**And** remains on HomePage

## Scenario: Inspector collapse state persists across navigation
**Given** the user expands the Prompts inspector  
**When** the user navigates away from ExecutionPage  
**And** returns  
**Then** the Prompts inspector remains expanded

---

# 6. Navigation Error Handling

## Scenario: Navigation errors show a global banner on ExecutionPage
**Given** the editor contains valid CNL  
**And** the backend mock response indicates an IR error  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** displays a global error banner  
**And** displays inline inspector errors

## Scenario: Navigation errors do not clear automatically
**Given** the user is on ExecutionPage  
**And** a navigation error banner is visible  
**When** the user navigates to EditorPage  
**And** returns to ExecutionPage  
**Then** the error banner remains visible

---

# 7. State Persistence

## Scenario: Editor text persists across navigation
**Given** the user enters text in the editor  
**When** the user navigates to HomePage  
**And** returns to EditorPage  
**Then** the editor text remains unchanged

## Scenario: Inspector selection persists across navigation
**Given** the user selects an IR node  
**When** the user navigates to EditorPage  
**And** returns to ExecutionPage  
**Then** the same IR node remains selected

---

# Summary

This BDD navigation specification defines deterministic Given/When/Then interactions for all navigation behaviors in Limelight‑X.  
It covers page transitions, sidebar navigation, navigation guards, modal blocking, inspector constraints, navigation‑specific errors, and state persistence.  
Backend responses are mocked, scenarios use medium granularity, and naming is behavioral.  
This navigation model is authoritative and must be followed exactly.