# API Module

## Purpose
This document defines the `/src/api` module: a local HTTP server that wraps the existing `run`, `explain`, and `trace` pipeline operations so that the `/ui` Avalonia client can invoke them without linking against Rust directly.

This feature affects:
- the **CLI** — it adds one new command, `llx serve`
- a **new module**, `/src/api`, which orchestrates existing pipeline calls

It does **not** affect:
- the CNL grammar
- the raw AST (parser output)
- the normalized AST (normalizer output)
- the IR compiler
- the evaluator
- the model adapter

`/src/api` calls the same functions the CLI already calls for `run`/`explain`/`trace`; it does not reimplement, skip, or reorder any pipeline stage.

---

# 1. Overview

`/src/api` is an HTTP server, started by the new `llx serve` CLI command, that exposes three endpoints — `POST /run`, `POST /explain`, `POST /trace` — mirroring the three existing CLI commands. Each endpoint accepts CNL source text and returns the same structured data the corresponding CLI command prints, encoded as JSON.

It exists solely so the `/ui` client (a separate Avalonia/.NET desktop application, see `spec/ux/ui-architecture.md`) has a way to invoke the pipeline without embedding a Rust runtime. It introduces no new grammar, AST shapes, IR nodes, or evaluator behavior — it is a thin orchestration layer in front of functionality the CLI already has.

---

# 2. Requirements

## 2.1 Functional Requirements
- Must expose `POST /run`, `POST /explain`, `POST /trace` over HTTP.
- Must accept a JSON request body: `{ "source": "<CNL text>" }`.
- Must return the shared response envelope defined in §5 below, matching the schemas already established in `spec/ux/ui-data-contracts.md`.
- `/run` must parse → normalize → compile → evaluate → return only the final result.
- `/explain` must parse → normalize → compile → return raw AST + normalized AST, without evaluating (no model calls).
- `/trace` must parse → normalize → compile → evaluate → return raw AST, normalized AST, IR, prompts, model outputs, and final result.
- Must be started via `llx serve` (see §8) and run until interrupted.

## 2.2 Non‑Functional Requirements
- **Determinism**: no parallel request handling — requests are processed one at a time, in arrival order, matching CLAUDE.md §3.3 and `architecture.md` §6. No retries.
- **Local-only trust boundary**: binds to `127.0.0.1` only; never binds to `0.0.0.0` or any non-loopback address.
- **No authentication**: v0.1 relies entirely on the loopback binding as its trust boundary; see Non-Goals.
- **Fail-fast startup**: refuses to start if `ANTHROPIC_API_KEY` is unset (same requirement the model adapter already has, per `model-adapter.md` §2.3) or if the configured port is already in use.
- **No new pipeline behavior**: must call the identical parser/normalizer/IR compiler/evaluator/model adapter functions the CLI uses; must not duplicate or diverge from that logic.

---

# 3. Grammar (If Applicable)
Not applicable. This feature introduces no new CNL constructs.

---

# 4. Raw AST Specification (If Applicable)
Not applicable. `/src/api` does not produce or alter raw AST nodes; it serializes the existing raw AST (produced by `/src/parser`) to JSON for the `/explain` and `/trace` responses.

---

# 5. Normalized AST Specification (If Applicable)
Not applicable in the sense of new normalization rules. `/src/api` serializes the existing normalized AST (produced by `/src/normalizer`) to JSON using the `ast_node` shape already defined in `spec/ux/ui-data-contracts.md` §5.1.

---

# 6. IR Specification (If Applicable)
Not applicable. `/src/api` introduces no new IR operations; it serializes the existing IR (produced by `/src/ir`) to JSON using the `ir_operation` shape defined in `spec/ux/ui-data-contracts.md` §5.4.

---

# 7. Evaluator Semantics (If Applicable)
Not applicable. `/src/api` does not alter evaluator behavior. For `/run` and `/trace`, it invokes the existing evaluator exactly as `llx run`/`llx trace` do, one request at a time, and serializes the resulting prompts, model outputs, and final result.

---

# 8. CLI Behavior

### `llx serve [--port <N>]`
- Starts the `/src/api` HTTP server.
- Binds to `127.0.0.1:<N>`, where `<N>` defaults to **4747** if `--port` is not given.
- On startup, validates that `ANTHROPIC_API_KEY` is set and that the port is available; if either check fails, prints a human‑readable fatal error and exits immediately without starting the server.
- Prints a single startup line once bound, e.g. `Listening on http://127.0.0.1:4747`.
- Runs until interrupted (Ctrl+C / SIGINT), then shuts down cleanly, finishing any in‑flight request first.
- Does not appear in `llx explain` or `llx trace` output — it is a distinct server mode, not a pipeline operation.

This is the one new CLI command approved for v0.1 (see CLAUDE.md §5 and §1.1).

---

# 9. Examples

### `POST /run`
Request:
```json
{ "source": "Load the article from \"article.txt\".\nSummarize it." }
```
Response (see `spec/ux/ui-data-contracts.md` §4 for the full schema):
```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "data": {
    "final_result": { "text": "…", "content_type": "plain" }
  }
}
```

### `POST /explain`
Same request shape. Response contains `raw_ast` and `normalized_ast` only (§2 of `ui-data-contracts.md`) — no model calls are made.

### `POST /trace`
Same request shape. Response contains `raw_ast`, `normalized_ast`, `ir`, `prompts`, and `model_outputs` (§3 of `ui-data-contracts.md`).

---

# 10. Error Conditions

Every error object in the response envelope (`ui-data-contracts.md` §1) includes a fixed `code` and `category` alongside `message`/`severity`/`location`. This table is the single authoritative source for that mapping — `ui-data-contracts.md` and `ui-viewmodels.md` both point back here rather than redefining it.

| Condition | `code` | `category` | `severity` | HTTP |
|---|---|---|---|---|
| Malformed JSON request body | `ERR_MALFORMED_REQUEST` | `api` | `error` | 400 |
| Missing `source` field | `ERR_MISSING_FIELD` | `api` | `error` | 400 |
| CNL parse failure (parser stage) | `ERR_CNL_PARSE` | `pipeline` | `error` | 200 |
| CNL normalization failure (normalizer stage) | `ERR_CNL_NORMALIZE` | `pipeline` | `error` | 200 |
| Evaluator fatal error (e.g. missing file) | `ERR_EVALUATOR_FATAL` | `pipeline` | `fatal` | 200 |
| Model adapter failure (any `Error::ModelAdapter*` variant, `model-adapter.md` §5) | `ERR_MODEL_ADAPTER` | `pipeline` | `fatal` | 200 |

Pipeline-level rows (200) are not transport failures — they mean the request was received and processed, but the pipeline itself failed. `location` is populated for parser/normalizer failures when available, and omitted otherwise. Evaluator/model-adapter fatal rows include the operation index in `message`, per `architecture.md` §7.

**Rust error → wire mapping rule**: every internal `Error` variant surfaced to `/src/api` maps to exactly one row above by error class (not by variant name) — e.g. all four `ModelAdapter*` variants (`NetworkError`, `InvalidResponse`, `MalformedResponse`, `HttpError`) map to `ERR_MODEL_ADAPTER`; the variant's `Display` text becomes the wire `message` verbatim, so the distinguishing detail (network vs. malformed vs. HTTP status) is preserved in text even though the code is shared.

**Malformed-body handling**: the HTTP framework's default JSON-rejection response must be overridden so that even a syntactically invalid body returns the standard envelope shape above (`ERR_MALFORMED_REQUEST`) — never a framework-default error body. The envelope shape is identical for every response, regardless of status code.

### Server Startup Errors (not HTTP responses)

| Condition | Response |
|---|---|
| Port already in use at startup | Fatal CLI error at `llx serve` startup; process exits before binding |
| Port unavailable for any other reason (e.g. bind permission denied) | Same as above — both "in use" and "permission denied" bind failures produce the same fatal startup error; the UI does not need to distinguish them, both handled by the existing `Category: Api, Severity: fatal` modal path (`ui-error-handling.md` §10) |
| `ANTHROPIC_API_KEY` unset at startup | Fatal CLI error at `llx serve` startup, same message as `model-adapter.md` §5.1; process exits before binding |

All error messages must be human‑readable, per CLAUDE.md §3.4.

---

# 11. BDD Scenarios
Acceptance criteria for this module are defined in **`spec/bdd-api.md`**, using the same extended GIVEN/WHEN/THEN/SO THAT/AS MEASURED BY format established in `spec/bdd.md`. That file is authoritative for this module's test scenarios.

---

# 12. Non‑Goals

`/src/api` does **not** support:
- authentication or authorization of any kind
- TLS / HTTPS
- binding to any non-loopback address
- concurrent/parallel request handling
- streaming responses
- request history or persistence across restarts
- new CNL grammar, AST nodes, IR nodes, or evaluator behavior
- remote access from another machine

These may be reconsidered in a future version if `/ui` needs to run on a different machine than `/src/api`.

---

# 13. Future Extensions

- Token-based authentication, if remote access is ever required
- Request queueing to allow bounded concurrency without violating determinism
- WebSocket or SSE streaming of trace events as they occur, rather than returning the full trace only on completion
- Configurable bind address for advanced/remote setups

---

# Summary
`/src/api` is a thin, local-only HTTP wrapper around the existing `run`/`explain`/`trace` pipeline operations, started via the new `llx serve` command and consumed exclusively by the `/ui` Avalonia client. It introduces no new grammar, AST, IR, or evaluator behavior — it only orchestrates calls to functionality the CLI already exposes, one request at a time, with the same determinism guarantees as the rest of Limelight‑X.
