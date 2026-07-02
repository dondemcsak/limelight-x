# BDD UI Error Cases

## Purpose
This document defines Behavior‑Driven Development (BDD) scenarios for **all UI error behaviors** in Limelight‑X.  
It covers validation errors, pipeline errors, API errors, rendering errors, navigation errors, and fatal errors.  
Scenarios use **mock backend responses**, **medium granularity**, and **pure Given/When/Then** format.  
The document is organized by **workflow** (Load, Edit, Run, Explain, Trace).

Error surfaces covered:
- Inline errors  
- Global error banner  
- Modal dialogs (fatal only)

Severity levels covered:
- Warning  
- Error  
- Fatal  

Inspector error coverage:
- AST  
- Normalized AST  
- IR  
- Prompts  
- Model Outputs  
- Rendering errors  

Error persistence:
- Editor validation errors persist until corrected  
- Other errors do not persist unless part of inspector state  

Error recovery:
- Retry clears inline + banner errors  
- Modal dialogs require acknowledgment  

Keyboard shortcuts do **not** trigger error scenarios.

---

# 1. Load Workflow Error Cases

## Scenario: Loading an invalid file shows an inline error
**Given** the user is on HomePage  
**And** an invalid `.llx` file exists  
**When** the user selects the file  
**Then** an inline error appears above the file selector  
**And** the UI remains on HomePage

## Scenario: Loading a file with unreadable content shows a global banner
**Given** the user is on HomePage  
**And** the file contains unreadable content  
**When** the user selects the file  
**Then** a global error banner appears  
**And** the UI remains on HomePage

## Scenario: Fatal file load error shows a modal dialog
**Given** the user is on HomePage  
**And** a fatal file load error occurs  
**When** the user selects the file  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 2. Edit Workflow Error Cases

## Scenario: Invalid syntax produces inline validation errors
**Given** the user is on EditorPage  
**And** the editor contains valid CNL  
**When** the user types invalid syntax  
**Then** inline validation errors appear  
**And** Run/Explain/Trace are disabled

## Scenario: Validation errors persist until corrected
**Given** the editor contains invalid CNL  
**When** the user navigates to HomePage  
**And** returns to EditorPage  
**Then** the validation errors remain visible

## Scenario: Rendering error in editor shows a global banner
**Given** the user is on EditorPage  
**And** a rendering error occurs  
**When** the editor attempts to display content  
**Then** a global error banner appears  
**And** the editor remains visible

## Scenario: Fatal editor rendering error shows a modal dialog
**Given** the user is on EditorPage  
**And** a fatal rendering error occurs  
**When** the editor attempts to display content  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 3. Run Workflow Error Cases

## Scenario: Validation errors block pipeline execution
**Given** the editor contains invalid CNL  
**When** the user presses Ctrl+R  
**Then** inline validation errors appear  
**And** no backend request is sent  
**And** the UI remains on EditorPage

## Scenario: Pipeline error shows inspector inline errors and a global banner
**Given** the editor contains valid CNL  
**And** the backend mock response indicates pipeline failure  
**When** the user presses Ctrl+R  
**Then** the UI navigates to ExecutionPage  
**And** inspector inline errors appear  
**And** a global error banner appears

## Scenario: API error during Run shows a global banner
**Given** the editor contains valid CNL  
**And** the backend mock response indicates an API error  
**When** the user presses Ctrl+R  
**Then** the UI navigates to ExecutionPage  
**And** a global error banner appears  
**And** inspectors show no content

## Scenario: Fatal pipeline error shows a modal dialog
**Given** the editor contains valid CNL  
**And** the backend mock response indicates a fatal error  
**When** the user presses Ctrl+R  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 4. Explain Workflow Error Cases

## Scenario: AST parsing error shows inline inspector errors
**Given** the editor contains valid CNL  
**And** the backend mock response indicates AST parsing failure  
**When** the user presses Ctrl+E  
**Then** the UI navigates to ExecutionPage  
**And** inline AST errors appear  
**And** a global error banner appears

## Scenario: Normalized AST rendering error shows inline inspector errors
**Given** the backend mock response includes malformed normalized AST  
**When** the user presses Ctrl+E  
**Then** the UI navigates to ExecutionPage  
**And** inline normalized AST errors appear  
**And** a global error banner appears

## Scenario: Fatal AST error shows a modal dialog
**Given** the editor contains valid CNL  
**And** the backend mock response indicates a fatal AST error  
**When** the user presses Ctrl+E  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 5. Trace Workflow Error Cases

## Scenario: IR generation error shows inline inspector errors
**Given** the editor contains valid CNL  
**And** the backend mock response indicates IR generation failure  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** inline IR errors appear  
**And** a global error banner appears

## Scenario: Prompt generation error shows inline inspector errors
**Given** the backend mock response includes malformed prompts  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** inline prompt errors appear  
**And** a global error banner appears

## Scenario: Model output rendering error shows inline inspector errors
**Given** the backend mock response includes malformed model outputs  
**When** the user presses Ctrl+T  
**Then** the UI navigates to ExecutionPage  
**And** inline model output errors appear  
**And** a global error banner appears

## Scenario: Fatal IR error shows a modal dialog
**Given** the editor contains valid CNL  
**And** the backend mock response indicates a fatal IR error  
**When** the user presses Ctrl+T  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 6. Navigation Error Cases

## Scenario: Navigation guard error shows a modal dialog
**Given** the user is on EditorPage  
**And** no pipeline has been executed  
**When** the user selects ExecutionPage in the sidebar  
**Then** a navigation guard modal appears  
**And** the UI remains on EditorPage

## Scenario: Fatal navigation error shows a modal dialog
**Given** the user is on EditorPage  
**And** a fatal navigation error occurs  
**When** the user attempts to navigate  
**Then** a modal dialog appears  
**And** all actions are disabled until acknowledged

---

# 7. Error Recovery

## Scenario: Retry clears inline and banner errors
**Given** the user is on ExecutionPage  
**And** inline inspector errors are visible  
**And** a global error banner is visible  
**When** the user retries the pipeline  
**Then** all inline errors clear  
**And** the global banner clears

## Scenario: Modal dialog requires acknowledgment
**Given** a modal dialog is visible  
**When** the user retries the pipeline  
**Then** the modal remains visible  
**And** actions remain disabled  
**When** the user acknowledges the modal  
**Then** actions become enabled

---

# Summary

This BDD error‑cases specification defines deterministic Given/When/Then interactions for all UI error behaviors in Limelight‑X.  
It covers validation, pipeline, API, rendering, navigation, and fatal errors across all workflows.  
Backend responses are mocked, scenarios use medium granularity, and naming is behavioral.  
This error‑case model is authoritative and must be followed exactly.