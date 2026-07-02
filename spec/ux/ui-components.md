# UI Components

## Purpose
This document defines all UI components used in the Limelight‑X Avalonia workflow dashboard.  
It specifies the structure, responsibilities, inputs, outputs, and deterministic behavior of each component.  
This specification is authoritative.  
All implementation must follow this component catalog exactly.

The UI is designed for analysts and citizen developers.  
Components must be simple, deterministic, and aligned with the Limelight‑X pipeline and HTTP API.

---

# 1. Overview

Limelight‑X UI components fall into four categories:

1. **Structural Components**  
   Pages, navigation, and layout primitives.

2. **Editor Components**  
   CNL editor, syntax highlighter, validation overlay, auto‑completion, hover tooltips, quick‑fix suggestions.

3. **Inspector Components**  
   Collapsible panels with **custom rendering logic**:
   - Raw AST (modern tree view)
   - Normalized AST (modern tree view)
   - IR (operation cards)
   - Prompts (prompt blocks)
   - Model Outputs (syntax‑highlighted Markdown)
   - Final Result (syntax‑highlighted Markdown)

4. **Utility Components**  
   Buttons, banners, loading indicators, error surfaces.

All components must be:

- deterministic  
- MVVM‑driven  
- stateless except for ephemeral UI state (collapse/expand)  
- free of business logic  
- fully documented in this specification  

---

# 2. Structural Components

## 2.1 `HomePage`
### Purpose
Entry point for the application.

### Responsibilities
- Display recent `.llx` files  
- Provide “Open File” action  
- Route to `EditorPage`

### Inputs
- `RecentFilesViewModel`

### Outputs
- Navigation events

---

## 2.2 `EditorPage`
### Purpose
Primary editing surface for CNL programs.

### Responsibilities
- Display CNL editor  
- Show inline validation errors  
- Provide Run / Explain / Trace actions  
- Route to `ExecutionPage`

### Inputs
- `EditorViewModel`

### Outputs
- Navigation events  
- Editor text changes  
- Validation results

---

## 2.3 `ExecutionPage`
### Purpose
Displays pipeline results in a vertical timeline.

### Responsibilities
- Render inspector components  
- Display execution status  
- Surface backend errors  
- Allow panel collapse/expand

### Inputs
- `PipelineExecutionViewModel`

### Outputs
- Inspector state changes

---

## 2.4 `NavigationBar`
### Purpose
Provides deterministic navigation between pages.

### Responsibilities
- Display navigation items  
- Reflect current page  
- Emit navigation commands

### Inputs
- `NavigationViewModel`

### Outputs
- Navigation events

---

# 3. Editor Components

## 3.1 `CnlEditor`
### Purpose
Text editor for `.llx` files.

### Responsibilities
- Render CNL text  
- Provide syntax highlighting  
- Provide auto‑formatting  
- Provide auto‑completion  
- Provide hover tooltips  
- Provide quick‑fix suggestions  
- Emit text changes  
- Display inline validation errors

### Inputs
- `EditorViewModel.Text`  
- `EditorViewModel.ValidationErrors`  
- `EditorViewModel.CompletionItems`  
- `EditorViewModel.QuickFixes`

### Outputs
- `TextChanged` events  
- Formatting commands  
- Completion selection events  
- Quick‑fix application events

### Deterministic Behavior
- Formatting must be deterministic  
- Highlighting must follow grammar rules  
- Validation must reflect `/explain` output exactly  
- Auto‑completion must be deterministic and grammar‑driven  
- Quick‑fix suggestions must be rule‑based and deterministic

---

## 3.2 `SyntaxHighlighter`
### Purpose
Tokenizes CNL text and applies styles.

### Responsibilities
- Highlight:
  - keywords  
  - pronouns  
  - resources  
  - expression holes  
  - quoted strings  
- Emit token spans

### Inputs
- Raw CNL text

### Outputs
- Token spans

### Deterministic Behavior
- No nondeterministic styling  
- No heuristic tokenization

---

## 3.3 `ValidationOverlay`
### Purpose
Displays inline validation errors.

### Responsibilities
- Render error markers  
- Display error messages on hover  
- Align markers with text positions

### Inputs
- `EditorViewModel.ValidationErrors`

### Outputs
- Hover events

---

## 3.4 `CompletionPopup`
### Purpose
Displays auto‑completion suggestions.

### Responsibilities
- Render completion list  
- Highlight selected item  
- Emit selection events

### Inputs
- `EditorViewModel.CompletionItems`

### Outputs
- Completion selection events

---

## 3.5 `QuickFixPopup`
### Purpose
Displays quick‑fix suggestions.

### Responsibilities
- Render fix suggestions  
- Emit fix application events

### Inputs
- `EditorViewModel.QuickFixes`

### Outputs
- Quick‑fix application events

---

# 4. Inspector Components

Inspector components appear in a **vertical pipeline timeline** on the `ExecutionPage`.  
Each inspector is collapsible and deterministic.  
Each inspector uses **custom rendering logic**.

---

## 4.1 `RawAstPanel`
### Purpose
Displays the raw AST returned by `/explain` or `/trace`.

### Rendering Style
- Modern tree view (VS Code style)  
- Chevron expanders  
- Indentation guides  
- Key/value pairs  
- Syntax coloring  
- Deterministic ordering

### Responsibilities
- Render raw AST tree  
- Support collapse/expand  
- Display errors if AST is missing or malformed

### Inputs
- `RawAstViewModel.Tree`  
- `RawAstViewModel.Error`

### Outputs
- Collapse/expand state

---

## 4.2 `NormalizedAstPanel`
### Purpose
Displays the normalized AST.

### Rendering Style
Same as Raw AST Panel, with normalized AST rules applied.

### Responsibilities
- Render normalized AST tree  
- Support collapse/expand  
- Display errors

### Inputs
- `NormalizedAstViewModel.Tree`  
- `NormalizedAstViewModel.Error`

### Outputs
- Collapse/expand state

---

## 4.3 `IrPanel`
### Purpose
Displays the IR list.

### Rendering Style
- Operation cards  
- Each card shows:
  - Operation type  
  - Input `$N` reference  
  - Prompt (if any)  
  - Target (if any)  
- Expandable details section  
- Syntax‑highlighted fields

### Responsibilities
- Render IR operation cards  
- Support collapse/expand  
- Display errors

### Inputs
- `IrViewModel.Operations`  
- `IrViewModel.Error`

### Outputs
- Collapse/expand state

---

## 4.4 `PromptPanel`
### Purpose
Displays constructed prompts (trace mode only).

### Rendering Style
- Prompt blocks  
- Syntax‑highlighted Markdown  
- Operation index labels  
- Deterministic formatting

### Responsibilities
- Render prompts in order  
- Support collapse/expand  
- Display errors

### Inputs
- `PromptViewModel.Prompts`  
- `PromptViewModel.Error`

### Outputs
- Collapse/expand state

---

## 4.5 `ModelOutputPanel`
### Purpose
Displays model outputs (trace mode only).

### Rendering Style
- Syntax‑highlighted Markdown  
- JSON syntax highlighting  
- Table rendering for pipe‑delimited Markdown  
- Deterministic formatting

### Responsibilities
- Render model outputs  
- Support collapse/expand  
- Display errors

### Inputs
- `ModelOutputViewModel.Outputs`  
- `ModelOutputViewModel.Error`

### Outputs
- Collapse/expand state

---

## 4.6 `FinalResultPanel`
### Purpose
Displays the final result of `/run`.

### Rendering Style
Same as Model Output Panel.

### Responsibilities
- Render final result  
- Support collapse/expand  
- Display errors

### Inputs
- `PipelineExecutionViewModel.FinalResult`  
- `PipelineExecutionViewModel.Error`

### Outputs
- Collapse/expand state

---

# 5. Utility Components

## 5.1 `PrimaryButton`
### Purpose
Standardized action button.

### Responsibilities
- Execute commands  
- Display loading state  
- Display disabled state

### Inputs
- Command  
- Loading flag  
- Enabled flag

### Outputs
- Click events

---

## 5.2 `ErrorBanner`
### Purpose
Displays global errors.

### Responsibilities
- Render error message  
- Provide dismiss action  
- Display severity

### Inputs
- Error text  
- Severity

### Outputs
- Dismiss events

---

## 5.3 `LoadingIndicator`
### Purpose
Displays loading state during backend calls.

### Responsibilities
- Render spinner + text  
- Display optional context

### Inputs
- Loading flag  
- Optional text

### Outputs
- None

---

## 5.4 `CollapsiblePanel`
### Purpose
Reusable component for inspector sections.

### Responsibilities
- Render header  
- Toggle collapse/expand  
- Animate deterministically  
- Render child content

### Inputs
- Title  
- Collapsed flag  
- Child content

### Outputs
- Collapse/expand events

---

# 6. Deterministic Behavior Requirements

All components must follow these rules:

1. **No nondeterministic animations**  
2. **No hidden state**  
3. **No implicit transitions**  
4. **All state must be derived from ViewModels**  
5. **All inspector content must match backend output exactly**  
6. **Editor formatting must be deterministic**  
7. **Syntax highlighting must follow grammar rules exactly**  
8. **Auto‑completion must be deterministic and grammar‑driven**  
9. **Quick‑fix suggestions must be deterministic and rule‑based**

---

# 7. Error Handling

Components must surface errors clearly and deterministically:

- Missing AST  
- Malformed AST  
- Missing IR  
- Malformed IR  
- Missing prompts  
- Missing model outputs  
- Backend HTTP errors  
- Validation errors

Errors must be:

- human‑readable  
- visible  
- non‑blocking (except fatal errors)

---

# 8. Non‑Goals

Components do **not** support:

- plugins  
- custom inspector types beyond those defined  
- multi‑file project views  
- nondeterministic animations  
- direct Rust integration  
- pipeline orchestration  
- macOS‑specific UI behavior (v0.1)

---

# 9. Future Extensions

Potential future components:

- IR graph visualizer  
- AST tree viewer with semantic annotations  
- Prompt diff viewer  
- Multi‑file project explorer  
- macOS‑specific UI variants  
- Plugin‑based inspectors

---

# Summary

This document defines all UI components used in the Limelight‑X workflow dashboard.  
Components are deterministic, MVVM‑driven, and aligned with the Limelight‑X pipeline and HTTP API.  
Inspector panels use custom rendering logic to make AST, IR, prompts, and model outputs approachable for analysts and citizen developers.  
All behavior is spec‑driven and must follow this specification exactly.