# UI Error Handling (Streaming Edition)

## Purpose
This document defines the complete error‑handling model for the Limelight‑X UI.  
It specifies how errors are surfaced, categorized, rendered, and cleared under the **event‑streaming API**.  
This specification is authoritative.  
All implementation must follow this behavior exactly.

The UI receives incremental JSON events over WebSocket.  
Errors may occur during HTTP request submission, WebSocket streaming, or pipeline execution.  
All errors must be surfaced deterministically and immediately.

---

# 1. Architectural Principles

1. **Deterministic Error Behavior**  
   - Errors must appear immediately.  
   - Errors must never be hidden or delayed.  
   - Error rendering must be deterministic.

2. **MVVM Purity**  
   - Views contain no logic.  
   - ViewModels contain error state.  
   - Services surface backend errors.

3. **Streaming‑Aware Error Handling**  
   - Errors may arrive as `pipeline_failed` events.  
   - Errors may occur before streaming begins (HTTP errors).  
   - Errors may occur during streaming (WebSocket disconnect).

4. **Strict Single Execution**  
   - Errors must stop the active pipeline.  
   - Execution buttons re‑enable only after error resolution.

---

# 2. Error Sources

Errors may originate from:

### 2.1 Backend Pipeline Errors
- parser errors  
- normalizer errors  
- IR compiler errors  
- evaluator errors  
- model adapter errors  

These arrive via `pipeline_failed` events.

### 2.2 API Errors
- malformed request  
- missing `source` field  
- invalid JSON  
- backend startup failure  
- port binding failure  

### 2.3 Transport Errors
- HTTP request failure  
- WebSocket disconnect  
- malformed event frame  
- invalid JSON event payload  

### 2.4 UI Errors
- invalid settings  
- invalid file path  
- editor validation errors  

### 2.5 Persistent Logging
Every `UiError` surfaced through §2.1-2.4 — i.e. everything added to `FileLoaderViewModel.Errors`, `EditorViewModel.ValidationErrors`, `PipelineExecutionViewModel.Errors`, and `SettingsViewModel.Errors` — is also logged via `Microsoft.Extensions.Logging`'s `ILogger`, at the `LogLevel` mapped from the error's `Severity` (`Info`→`Information`, `Warning`→`Warning`, `Error`→`Error`, `Fatal`→`Critical`), to the file described in `ui-deployment.md` §4.3.

This is purely additive to the existing UI surfacing rules above — it never changes what the user sees or where. A failure to write the log entry itself (unwritable directory, disk full, etc.) must not surface as a `UiError`, must not crash the app, and must not block the original error from reaching its normal UI surface (banner/inline/inspector) — logging failures fail silently.

---

# 3. Error Envelope (Streaming)

All backend errors arrive in the standard envelope:

```json
{
  "version": "v1",
  "success": false,
  "errors": [
    {
      "code": "ERR_CNL_PARSE",
      "category": "pipeline",
      "severity": "error",
      "message": "Unexpected token at line 3",
      "location": { "line": 3, "column": 14 }
    }
  ],
  "event_type": "pipeline_failed",
  "correlation_id": "abc-123",
  "data": {}
}
```

### Rules
- Envelope shape is identical for all events.  
- UI must ignore events with mismatched correlation IDs.  
- UI must surface errors immediately.

---

# 4. Error Categories

| Category | Description | Origin |
|----------|-------------|--------|
| `api` | malformed request, missing fields, invalid JSON | Wire — sent by the server in an error object's `category` field |
| `pipeline` | parser, normalizer, IR, evaluator, model adapter | Wire — sent by the server in an error object's `category` field |
| `transport` | HTTP request failure, WebSocket disconnect, malformed event frame, invalid JSON event payload | Client-synthesized — never sent by the server |
| `ui` | invalid settings, invalid file path, editor validation errors | Client-synthesized — never sent by the server |

`api` and `pipeline` are the only values that appear in a server-sent error object's `category` field (see `ui-data-contracts.md` §3). `transport` and `ui` errors have no wire representation — the client constructs a `UiError` locally (e.g. on a `ClientWebSocket` disconnect, a malformed event payload, or a local rendering/validation failure) and assigns one of these two categories itself.

---

# 5. Error Severity

| Severity | Meaning |
|----------|---------|
| `error` | recoverable; pipeline stops but UI remains stable |
| `fatal` | unrecoverable; evaluator or model adapter fatal error |

### Rules
- Fatal errors must disable inspector updates.  
- Fatal errors must re‑enable execution buttons.  
- Fatal errors must remain visible until dismissed.

---

# 6. Error Rendering Locations

Errors must appear in the following locations:

### 6.1 Global Error Banner
- Appears at top of Execution Page.  
- Shows first error message.  
- Expands to show full error list.  
- Dismissible.

### 6.2 Inline Editor Errors
- Parser/grammar errors from `/explain`.  
- Highlighted in the editor.  
- Displayed in the margin and error list.

All inline editor errors arrive over the wire as a single code/category: `ERR_CNL_PARSE`/`pipeline` (see `api.md` §10). The UI nonetheless distinguishes three presentation kinds, derived client-side from the error's `message` and `location` — there is no separate wire code for each:

| Kind | How the UI recognizes it |
|------|---------------------------|
| Parser error | Default: any `ERR_CNL_PARSE` error whose `location` does not fall inside a `{{ prompt: "..." }}` span. |
| Grammar error | An `ERR_CNL_PARSE` error whose `message` identifies a specific grammar-rule violation (e.g. an unexpected token type named in `cnl-grammar.md`). |
| Hole error | An `ERR_CNL_PARSE` error whose `location` falls inside a `{{ prompt: "..." }}` expression-hole span, per the `PromptHole` grammar in `cnl-grammar.md` §4. |

This classification only affects which marker/message the editor shows; it does not change the error's `code`, `category`, or `severity`.

### 6.3 Inspector Errors
- If an inspector’s data cannot be rendered, show an inline error panel.  
- Inspector remains visible but marked as failed.

### 6.4 Execution Page Errors
- Pipeline errors appear immediately.  
- Execution buttons re‑enable.

---

# 7. Error Handling Workflow

### 7.1 HTTP Request Errors
If the initial `POST /run` / `/explain` / `/trace` fails:

1. Execution does not begin.  
2. No navigation occurs.  
3. Error banner appears on Editor Page.  
4. Execution buttons remain enabled.

### 7.2 Streaming Errors (WebSocket)
If WebSocket disconnects during execution:

1. PipelineExecutionViewModel sets `HasErrors = true`.  
2. Global error banner appears.  
3. Execution buttons re‑enable.  
4. Inspectors stop updating.

### 7.3 Pipeline Errors (`pipeline_failed`)
When a `pipeline_failed` event arrives:

1. Execution stops.  
2. Inspectors remain visible.  
3. Error banner appears.  
4. Execution buttons re‑enable.  
5. Navigation remains on Execution Page.

### 7.4 Fatal Errors
Fatal errors (evaluator/model adapter) must:

- disable inspector updates  
- show fatal styling  
- prevent further streaming  
- re‑enable execution buttons  
- remain visible until dismissed

### 7.5 Settings Backend Restart Errors
Saving Settings relaunches `llx serve` (see `ui-viewmodels.md` SettingsViewModel). If the relaunch fails (port unavailable, `ANTHROPIC_API_KEY` unset, or the process exits before printing its listening line):

1. The failure is synthesized client-side as a `UiError` with `category = "api"` (it reflects the backend's own fail-fast startup checks, per `api.md` §2.2, not a transport or UI-local failure).  
2. `ErrorBannerViewModel.IsVisible` becomes `true`, showing the failure message.  
3. The user remains on the Settings Page — this is the one case where an error does not imply the Execution/Editor error-handling flow above, since no pipeline execution was in progress.  
4. The previous backend connection (if any) is left running until a restart succeeds, so an in-flight execution is not abandoned by a bad Settings save.

---

# 8. Error Clearing Rules

Errors clear when:

- a new execution begins (`pipeline_started`)  
- the user dismisses the global banner  
- the user navigates away from Execution Page  
- the editor performs a new validation pass

### Rules
- Clearing must be deterministic.  
- Clearing must not hide active errors.  
- Clearing must not occur automatically during streaming.

---

# 9. Error ViewModel Contracts

### 9.1 ErrorBannerViewModel
State:
- `IsVisible : bool`
- `Errors : ObservableCollection<Error>`
- `Severity : string`

Commands:
- `DismissCommand`

### 9.2 EditorErrorViewModel
State:
- `Line : int`
- `Column : int`
- `Message : string`
- `Severity : string`

### 9.3 InspectorErrorViewModel
State:
- `Message : string`
- `Severity : string`
- `IsCollapsed : bool`

---

# 10. Error Styling

### 10.1 Colors
- Error: **red**  
- Fatal: **dark red**  
- Active inspector: **lime** (unchanged)  
- Collapsed inspector: neutral gray  

### 10.2 Banner Styling
- Red background  
- White text  
- Expandable details section  
- Dismiss button

### 10.3 Inline Editor Styling
- Red underline  
- Red margin marker  
- Tooltip with error message

---

# 11. Navigation & Error Interaction

### Forbidden Transitions During Errors
- Errors must not navigate away from Execution Page.  
- Errors must not navigate automatically to Editor Page.  
- Errors must not hide inspector state.

### Allowed Transitions After Errors
- User may navigate manually after dismissing or acknowledging errors.  
- Execution buttons re‑enable.

---

# 12. Non‑Goals

Error handling does **not** support:

- automatic retries  
- parallel executions  
- queued executions  
- cancellation  
- nondeterministic animations  
- custom error categories  
- UI-side pipeline reconstruction  

---

# 13. Future Extensions

Potential enhancements:

- richer error diagnostics  
- structured error diffing  
- error history panel  
- per‑inspector error timelines  
- additional observability events (timing, resource usage)

---

# Summary

Limelight‑X error handling is deterministic, MVVM‑pure, and fully aligned with the streaming API.  
Errors appear immediately, stop the active pipeline, and update the UI in real time.  
The global banner, inspector errors, and inline editor errors provide a consistent, predictable error experience across the entire workflow.