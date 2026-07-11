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

4. **App‑Wide Single Execution**  
   - Errors must stop the active pipeline (in the tab that owns it).  
   - Execution buttons re‑enable app‑wide only after error resolution.

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
Every `UiError` surfaced through §2.1-2.4 — i.e. everything added to `WorkspaceViewModel.Errors`, a tab's `PipelineExecutionViewModel.Errors`, and `SettingsViewModel.Errors` — is also logged via `Microsoft.Extensions.Logging`'s `ILogger`, at the `LogLevel` mapped from the error's `Severity` (`Info`→`Information`, `Warning`→`Warning`, `Error`→`Error`, `Fatal`→`Critical`), to the file described in `ui-deployment.md` §4.3. `EditorViewModel` has no error collection of its own to log (`bdd-ui-interactions.md` §2.2) — its only state is `LocalDiagnostics`, which is advisory Tree‑sitter output, not a `UiError`.

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

### 6.1 Tab Error Banner
- Appears at top of the active tab's execution panel.  
- Scoped to that tab only — switching to another tab shows that other tab's own banner state, not this one's.  
- Shows first error message.  
- Expands to show full error list.  
- Dismissible.

### 6.2 Inline Editor Errors
- The editor's only error surface while the user is typing is Tree‑sitter's local `LocalDiagnostics` (advisory, `cnl-editor-architecture.md` §5, `bdd-ui-interactions.md` §2.16–§2.17) — the squiggly-underline-plus-hover-tooltip experience. There is no backend call, and no separate editor-level error collection, while no Run/Explain click is in flight (`bdd-ui-interactions.md` §2.2) — `EditorViewModel` has no `IPipelineService`/`IEventStreamService` dependency at all.
- `/explain`'s `ERR_CNL_PARSE` errors (`pipeline`/`ERR_CNL_PARSE`, see `api.md` §10) are only ever produced by an explicit Run or Explain click, and surface exclusively through `PipelineExecutionViewModel.ErrorBanner` (§6.1) in the execution panel — the same path, and the same styling, as any other pipeline error. Nothing reconciles them against `LocalDiagnostics`; the two remain independent, per `cnl-editor-architecture.md` §5's "two independent parsers" model.

### 6.3 Inspector Errors
- If an inspector’s data cannot be rendered, show an inline error panel.  
- Inspector remains visible but marked as failed.

### 6.4 Execution Panel Errors
- Pipeline errors appear immediately in the owning tab's execution panel.  
- Execution buttons re‑enable app‑wide.

---

# 7. Error Handling Workflow

### 7.1 HTTP Request Errors
If the initial `POST /trace` (Run) or `POST /explain` (Explain) fails:

1. Execution does not begin.  
2. No tab or workspace-area change occurs.  
3. Error banner appears in the tab that attempted execution.  
4. Execution buttons remain enabled (the app‑wide lock was never acquired).

### 7.2 Streaming Errors (WebSocket)
If WebSocket disconnects during execution:

1. That tab's `PipelineExecutionViewModel` sets `HasErrors = true`.  
2. That tab's error banner appears.  
3. Execution buttons re‑enable app‑wide.  
4. That tab's inspectors stop updating.

### 7.3 Pipeline Errors (`pipeline_failed`)
When a `pipeline_failed` event arrives:

1. Execution stops in the owning tab.  
2. That tab's inspectors remain visible.  
3. That tab's error banner appears.  
4. Execution buttons re‑enable app‑wide.  
5. The owning tab remains the active tab if it already was; no tab switch is forced.

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
2. `ErrorBannerViewModel.IsVisible` becomes `true`, showing the failure message inside the Settings modal.  
3. The Settings modal remains open — this is the one case where an error does not imply the per‑tab error-handling flow above, since no pipeline execution was in progress.  
4. The previous backend connection (if any) is left running until a restart succeeds, so an in-flight execution is not abandoned by a bad Settings save.

---

# 8. Error Clearing Rules

Errors are scoped to the tab that produced them. A tab's errors clear when:

- a new execution begins in that same tab (`pipeline_started`)  
- the user dismisses that tab's banner  
- that tab's editor performs a new validation pass

**Switching to, or away from, another tab never clears a tab's errors.** There is no equivalent of the old "navigating away from Execution Page clears the banner" rule — each tab's error state persists independently until the tab itself re‑executes or the user dismisses it, even while the user is looking at a different tab entirely.

### Rules
- Clearing must be deterministic.  
- Clearing must not hide active errors on other tabs.  
- Clearing must not occur automatically during streaming.  
- Clearing must never be triggered by switching the active tab.

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

This styling is shared by two data-model-separate, independently-triggered sources, per `bdd-ui-interactions.md` §2.7/§2.16:
- **Authoritative**: `PipelineExecutionViewModel.ErrorBanner`, populated only as a result of an explicit Run/Explain click (§6.1) — never inline in the editor.
- **Advisory**: `EditorViewModel.LocalDiagnostics`, sourced from Tree‑sitter `ERROR`/`MISSING` nodes as the user types, rendered by `LocalDiagnosticsRenderer` with the same red‑underline‑plus‑margin‑marker‑plus‑tooltip shape.

Sharing the visual style is deliberate (a Tree‑sitter error should read exactly like any other error to the user); the two never merge, and `EditorViewModel` has no backend-sourced error collection for `LocalDiagnostics` to write into (`bdd-ui-interactions.md` §2.2, §2.8).

---

# 11. Workspace & Error Interaction

### Forbidden Behavior During Errors
- Errors must not force a tab switch.  
- Errors must not close or open any tab automatically.  
- Errors must not hide inspector state.

### Allowed Behavior After Errors
- The user may switch tabs, open new tabs, or dismiss/acknowledge errors freely at any time — this was never blocked, since tab switching is not gated by execution or error state (`ui-routing-navigation.md` §7).  
- Execution buttons re‑enable app‑wide once the error's execution reaches a terminal state.

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