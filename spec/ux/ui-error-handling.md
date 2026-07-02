# UI Error Handling

## Purpose
This document defines the complete error‑handling system for the Limelight‑X Avalonia workflow dashboard.  
It specifies error categories, propagation rules, UI surfaces, severity mapping, recovery behavior, persistence, and rendering logic.  
This specification is authoritative.  
All implementation must follow this error‑handling model exactly.

Limelight‑X uses a unified error taxonomy, rich severity mapping, automatic recovery, local + global propagation, and modal dialogs for fatal errors.  
Errors persist across navigation until cleared or retried.

---

# 1. Unified Error Taxonomy

All errors in Limelight‑X use a unified category system with structured subtypes:

```
UiError {
    Code: string
    Message: string
    Severity: ErrorSeverity
    Category: ErrorCategory
    Location?: ErrorLocation
}
```

### ErrorSeverity
```
info
warning
error
fatal
```

### ErrorCategory
```
Validation
Pipeline
Api
Rendering
Navigation
Editor
State
```

### ErrorLocation (optional)
```
{
    line: number
    column: number
    span: { start: number, end: number }
}
```

### Rules
- All errors must include a **Code** (e.g., `ERR_AST_PARSE`, `ERR_IR_BUILD`).  
- All errors must include a **Message** (human‑readable).  
- All errors must include a **Severity**.  
- UI must ignore unknown fields (forward‑compatible).  

---

# 2. Error Surfaces

Limelight‑X uses three error surfaces:

1. **Inline errors**  
2. **Global error banner**  
3. **Modal dialogs**

Each surface is used deterministically based on severity and category.

---

# 3. Inline Errors

Inline errors appear **above component content**.

### Used For
- Validation errors (Editor, Settings)  
- Inspector errors (AST, IR, Prompts, Model Outputs)  
- Rendering errors inside inspectors  

### Behavior
- Component content remains visible below.  
- Inline errors use yellow (warning) or red (error) styling.  
- Inline errors clear automatically when the user retries the action.  
- Inline errors persist across navigation until cleared.

### Styling
```
Background: #1F1F1F
Border: #EF4444 (error) or #F59E0B (warning)
Text: TextPrimary
Icon: Fluent UI warning/error icon
```

---

# 4. Global Error Banner

The global banner appears **for any error**, except navigation failures.

### Used For
- Pipeline errors  
- API errors  
- Rendering errors  
- Inspector errors (in addition to inline errors)  
- State errors  

### Behavior
- Appears at top of page.  
- Does not block interaction.  
- Clears automatically on retry.  
- Persists across navigation until cleared.

### Styling
```
Background: #EF4444 (error) or #F59E0B (warning)
Text: #FFFFFF
Icon: Fluent UI error icon
Shadow: ShadowSmall
```

---

# 5. Modal Dialogs

Modal dialogs are used **only for navigation failures and fatal errors**.

### Used For
- Navigation guard failures  
- Fatal backend errors  
- Fatal rendering errors  
- Fatal state errors  

### Behavior
- Blocks interaction until acknowledged.  
- Disables all actions until user clicks “OK”.  
- Does not navigate automatically.  
- Does not clear automatically — user must acknowledge.

### Fatal Error Behavior
When a fatal error occurs:

1. A modal dialog is shown.  
2. All actions are disabled.  
3. The UI waits for user acknowledgment.  
4. After acknowledgment, the user may retry or navigate manually.  
5. ExecutionPage may be entered afterward if a retry succeeds.

### Styling
```
Background: Surface
Border: #EF4444
Text: TextPrimary
Accent: none (lime is never used for errors)
Buttons: PrimaryButton (accent muted)
```

---

# 6. Severity Mapping

Limelight‑X uses **rich severity mapping**:

| Severity | UI Surface | Behavior |
|----------|------------|----------|
| info     | inline     | non‑blocking |
| warning  | inline + yellow highlight | non‑blocking |
| error    | global banner + red highlight | blocks pipeline actions |
| fatal    | modal dialog + red highlight | blocks all actions |

---

# 7. Error Recovery

Errors clear automatically when the user retries the action.

### Rules
- Inline errors clear on retry.  
- Global banner clears on retry.  
- Modal dialogs clear only when user acknowledges.  
- Errors do not clear automatically on navigation unless retry occurs.

---

# 8. ExecutionPage Error Handling

When an inspector fails (e.g., malformed IR):

1. The inspector shows an inline error above its content.  
2. ExecutionPage shows a global error banner.  
3. Other inspectors remain visible.  
4. ExecutionPage remains navigable.  
5. Errors clear automatically on retry.

---

# 9. Editor Validation Error Behavior

Validation errors:

- Block Run/Explain/Trace.  
- Show inline errors above editor content.  
- Show a global banner if severity is `error` or higher.  
- Clear automatically when the user edits or retries.

---

# 10. Settings Validation & Apply Error Behavior

Settings field validation (`Category: Validation`, per §1) follows the same pattern as Editor validation:

- Blocks `SaveSettingsCommand`.  
- Shows inline errors above the relevant field (e.g. "Port must be between 1 and 65535", "API key is required", "Log path must be a valid absolute path").  
- Clears automatically when the user edits the field.

Field-level validation is syntactic only:
- `Port`: integer, 1–65535.  
- `ApiKey`: non-empty.  
- `LogPath`: syntactically valid absolute path — existence and writability are **not** checked at this stage.

`LogPath` existence/writability is checked when `llx serve` actually starts. If it can't open the log file, that surfaces as a relaunch failure (below), not a field validation error.

Applying settings (stopping and relaunching `llx serve` with the new port/key) is a distinct failure mode from field validation:

- A relaunch failure (e.g. the new port is unavailable, or the process crashes on startup) is `Category: Api, Severity: fatal` — it shows a **modal dialog**, per §5, disabling all actions until acknowledged.  
- On relaunch failure, the user's edited field values are **not discarded** (`SettingsViewModel.IsDirty` remains `true`), so the user can correct the value and retry without re-entering everything.  
- Unlike Editor validation errors, Settings validation errors do not block other pages — they only block leaving SettingsPage via Save (leaving via Cancel/Discard, per Guard 5, is unaffected by validation state).

---

# 11. Error Logging

Limelight‑X logs errors **only in memory**.

### Rules
- No file logging.  
- No developer console panel.  
- No persistence across sessions.  

Errors exist only in ViewModel state.

---

# 12. Error Propagation

Errors propagate both locally and globally.

### Rules
1. Components show inline errors.  
2. Parent ViewModels receive the error.  
3. The global error banner appears.  
4. A modal dialog appears only if severity is `fatal`.

---

# 13. Error Styling

Error styling follows:

- Red for errors  
- Yellow for warnings  
- Lime accent is **never** used for errors  
- Lime remains reserved exclusively for active states

---

# 14. Error Persistence Across Navigation

Errors persist across navigation until cleared or retried.

### Rules
- Editor errors persist.  
- Execution errors persist.  
- Errors clear only on retry or modal acknowledgment.

---

# 15. Backend Error Handling

If backend returns `success = false`:

1. The UI navigates to ExecutionPage.  
2. ExecutionPage shows a global error banner.  
3. Inspectors show inline errors.  
4. Fatal errors show a modal dialog and disable actions until acknowledged.

---

# 16. User-Facing Error Messages

Error messages use a hybrid format:

- A simple citizen‑developer‑friendly message  
- Technical details behind a collapsible “Details” section  
- Error code  
- Severity  
- Optional location

Example:

```
Error: Unable to parse AST.
Details:
  Code: ERR_AST_PARSE
  Line: 12
  Column: 5
  Span: 34–47
```

---

# Summary

Limelight‑X uses a unified error taxonomy, rich severity mapping, inline errors above content, global banners for all errors, and modal dialogs for navigation failures and fatal errors.  
Errors propagate locally and globally, persist across navigation, and clear automatically on retry.  
Fatal errors disable all actions until acknowledged.  
Backend failures navigate to ExecutionPage, where inspectors show inline errors and the page shows a global banner.  
All error messages use hybrid formatting with optional technical details.

This error‑handling model is deterministic and must be followed exactly.