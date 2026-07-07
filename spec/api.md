# API Module (Streaming Edition)

## Purpose
This document defines the `/src/api` module: a local HTTP + WebSocket server that wraps the existing `run`, `explain`, and `trace` pipeline operations so that the `/ui` Avalonia client can invoke them without linking against Rust directly.

This feature affects:
- the **CLI** — it adds one new command, `llx serve`
- a **new module**, `/src/api`, which orchestrates existing pipeline calls
- the **transport model** — pipeline results are now delivered as a **stream of JSON events**, not a single JSON response

It does **not** affect:
- the CNL grammar
- the raw AST (parser output)
- the normalized AST (normalizer output)
- the IR compiler
- the evaluator
- the model adapter

`/src/api` calls the same functions the CLI already calls for `run`/`explain`/`trace`; it does not reimplement, skip, or reorder any pipeline stage.  
Only the **transport layer** changes: results are streamed incrementally — meaning each stage's event is emitted as soon as that stage's pipeline function returns, and never held back until later stages (including the evaluator's model-adapter calls) have also finished. See §2.1 "Event Emission Timing" for the normative requirement this implies.

---

# 1. Overview

`/src/api` is an HTTP + WebSocket server, started by the new `llx serve` CLI command.

It exposes three HTTP endpoints:

- `POST /run`
- `POST /explain`
- `POST /trace`

Each endpoint accepts CNL source text and returns an immediate acknowledgment containing a `correlation_id`.

All actual pipeline results are delivered as **JSON events** over a WebSocket channel at:

```
ws://127.0.0.1:<port>/events
```

Each event uses the same envelope shape defined in `spec/ux/ui-data-contracts.md`, with two additions:

- `event_type`
- `correlation_id`

The UI receives results incrementally as the pipeline executes.

---

# 2. Requirements

## 2.1 Functional Requirements

### HTTP Request
- Must expose `POST /run`, `POST /explain`, `POST /trace`.
- Must accept a JSON request body: `{ "source": "<CNL text>" }`.
- Must return immediately with:

```json
{
  "accepted": true,
  "correlation_id": "<id>"
}
```

`correlation_id` must be unique per request (see `ui-data-contracts.md` §10); no specific format (e.g. UUID) is required.

### Streaming Response
All pipeline results must be emitted as **JSON events** over WebSocket.

### Event Types

| Pipeline Stage | Event Type |
|----------------|------------|
| Pipeline started | `pipeline_started` |
| Raw AST ready | `raw_ast_generated` |
| Normalized AST ready | `normalized_ast_generated` |
| IR ready | `ir_generated` |
| A prompt has been constructed for one model-adapter call | `prompt_generated` |
| A model-adapter call has returned its output | `model_output_generated` |
| Final result ready | `final_result_ready` |
| Any pipeline error | `pipeline_failed` |

`prompt_generated` and `model_output_generated` are emitted once **per model-calling IR operation** (`Extract`, `Summarize`, `Translate`, `Rewrite`, `Format`), not once per pipeline run — see "Per-operation rules" below.

### Per‑operation rules

- `/run` emits:  
  `pipeline_started` → `final_result_ready`

  (`/run` does not stream per-operation prompt/model-output visibility; that is `/trace`'s job.)

- `/explain` emits:  
  `pipeline_started` → `raw_ast_generated` → `normalized_ast_generated`

  (`/explain` never invokes the evaluator, so it produces no final result; the arrival of `normalized_ast_generated` is itself the completion signal for this endpoint.)

- `/trace` emits:  
  ```
  pipeline_started → raw_ast_generated → normalized_ast_generated → ir_generated →
  ( prompt_generated → model_output_generated ) × N
  → final_result_ready
  ```
  where N is the number of model-calling IR operations in the program (0 or more), each pair emitted in program order. A program consisting only of `Load` operations has N = 0 and goes straight from `ir_generated` to `final_result_ready`.

### Event Emission Timing

- Each event MUST be emitted immediately after its corresponding pipeline stage function returns, and BEFORE the next pipeline stage function is invoked:
  - `parser::parse` returns → emit `raw_ast_generated` → *then* call `normalizer::normalize`
  - `normalizer::normalize` returns → emit `normalized_ast_generated` → *then* call `ir::compiler::compile`
  - `ir::compiler::compile` returns → emit `ir_generated` → *then* call `evaluator::evaluate`
  - `evaluator::evaluate` returns → emit `final_result_ready`

- Unlike the stage functions above, `evaluator::evaluate` is not opaque with respect to events: per `evaluator-semantics.md` §4.1, `/src/api`'s worker must supply an `EvaluatorObserver` to `evaluator::evaluate`, and must forward each callback to the WebSocket **while `evaluate()` is still executing** — not by reading `prompts`/`model_outputs` out of the returned `EvalOutcome` after the fact. Concretely: `on_prompt_generated(i, ...)` fires → emit `prompt_generated` for operation `i` → *then* the evaluator calls `adapter.complete` for operation `i` → it returns → `on_model_output_generated(i, ...)` fires → emit `model_output_generated` for operation `i` → *then* the evaluator moves on to operation `i`'s successor (if any).

- The critical invariant — and the one that catches the "compute everything, then burst-fire" failure mode — is upstream of that: `ir_generated` (and, transitively, `raw_ast_generated` and `normalized_ast_generated`) MUST be observable on the WebSocket strictly before `evaluator::evaluate` begins executing, and therefore strictly before any model adapter call happens. An implementation is non-compliant if `ir_generated` arrives only after the model adapter has already been invoked, even if the final on-wire event order is otherwise correct.

- A second, equally critical invariant now applies **inside** `evaluate()`'s execution: for IR operation *i* that requires a model call, `prompt_generated` for operation *i* MUST be observable on the WebSocket strictly before the model adapter is invoked for operation *i*, and `model_output_generated` for operation *i* MUST be observable strictly after that call returns and strictly before the model adapter is invoked for operation *i+1* (if any exists). An implementation that only synthesizes `prompt_generated`/`model_output_generated` from the `EvalOutcome` returned once `evaluate()` fully completes is non-compliant, even if the resulting on-wire order happens to look correct — the same "compute everything, then burst-fire" failure mode this section already prohibits for the earlier stages.

- Error case: if the model adapter fails on operation *i*, `prompt_generated` for operation *i* will already have been emitted (it fires before the call), but `model_output_generated` for operation *i* must never be emitted, and no further stage events (including `final_result_ready`) are emitted — `pipeline_failed` is emitted instead, per §10.

- `final_result_ready` remains the last event for a successful run, emitted immediately after `evaluator::evaluate` fully returns.

- It is a spec violation to invoke two or more of `parser::parse`, `normalizer::normalize`, `ir::compiler::compile`, `evaluator::evaluate` inside a single opaque task/closure/future (e.g. one `tokio::task::spawn_blocking` body) and defer all of their corresponding events until that task/closure/future completes and is `.await`ed. Each stage function call and the sending of its event must be sequenced as independently observable steps. Because the observer callbacks in `evaluate()` fire synchronously during its execution, `evaluate()` itself may still run inside a single `spawn_blocking` body — the observer forwards events out of that blocking body as they occur, so the body being "opaque" to `.await` does not make its internal events opaque to the WebSocket.

- This requirement does not require changing the signatures of `parser::parse`, `normalizer::normalize`, or `ir::compiler::compile` (they remain plain synchronous `fn(...) -> Result<T, Error>`). `evaluator::evaluate` does gain a new optional `observer` parameter, per `evaluator-semantics.md` §4.1 — this is the one deliberate exception, needed precisely to satisfy the per-operation invariant above. The existing 7 stage events are replaced by 6 fixed events plus a repeating `prompt_generated`/`model_output_generated` pair (see "Per-operation rules" above), plus `pipeline_failed`.

### Envelope Shape
Every event uses the same envelope shape defined in `ui-data-contracts.md`:

```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "data": { ... },
  "event_type": "raw_ast_generated",
  "correlation_id": "<id>"
}
```

The envelope shape is identical for every event.

---

## 2.2 Non‑Functional Requirements

- **Determinism**:  
  No parallel request handling — requests are processed one at a time, in arrival order, matching CLAUDE.md §3.3 and `architecture.md` §6.  
  No retries: a failed request (at any stage) is reported once via `pipeline_failed` or a synchronous error response and is never automatically retried by the server.

- **Local-only trust boundary**:  
  Binds to `127.0.0.1` only; never binds to `0.0.0.0` or any non-loopback address.

- **No authentication**:  
  v0.1 relies entirely on loopback binding.

- **Fail-fast startup**:  
  Refuses to start if `ANTHROPIC_API_KEY` is unset or if the port is already in use.

- **No new pipeline behavior**:  
  Must call the identical parser/normalizer/IR compiler/evaluator/model adapter functions the CLI uses.

---

## 2.3 Streaming Channel

- WebSocket endpoint:  
  `ws://127.0.0.1:<port>/events`

- Every HTTP request allocates a `correlation_id`.

- Events for a given `correlation_id` must be emitted **in pipeline order**, AND each event must be emitted as its own stage completes — not held until later stages (including the evaluator's model-adapter calls) have also finished. See §2.1 "Event Emission Timing" for the precise rule. Pipeline *order* and emission *timing* are separate requirements: an implementation that computes every stage synchronously and only afterward fires all events in the correct order satisfies the first requirement but violates the second.

- Determinism rule:  
  Events for different correlation IDs never interleave; the server processes one request at a time.

- Serialization:  
  Events must be serialized using `serde_json::to_writer` to avoid building large in‑memory JSON structures.

---

# 3. Grammar (If Applicable)
Not applicable. This feature introduces no new CNL constructs.

---

# 4. Raw AST Specification (If Applicable)
Not applicable. `/src/api` does not produce or alter raw AST nodes; it streams the existing raw AST (produced by `/src/parser`) as JSON events.

---

# 5. Normalized AST Specification (If Applicable)
Not applicable. `/src/api` streams the existing normalized AST (produced by `/src/normalizer`) using the `ast_node` shape defined in `spec/ux/ui-data-contracts.md` §4.

---

# 6. IR Specification (If Applicable)
Not applicable. `/src/api` streams the existing IR (produced by `/src/ir`) using the `ir_operation` shape defined in `spec/ux/ui-data-contracts.md` §6.

---

# 7. Evaluator Semantics (If Applicable)
Not applicable. `/src/api` does not alter evaluator behavior — it invokes the existing evaluator exactly as `llx run`/`llx trace` do, one request at a time, supplying an `EvaluatorObserver` (per `evaluator-semantics.md` §4.1) so it can forward events as they occur. "Streams prompts, model outputs, and final result" means: `prompt_generated`/`model_output_generated` pairs are emitted incrementally *during* `evaluator::evaluate`'s execution, one pair per model-calling operation, in program order (see §2.1 "Event Emission Timing"); `final_result_ready` is emitted immediately after `evaluator::evaluate` fully returns; and — separately, and more importantly — `ir_generated` (and every stage event before it) is emitted before `evaluator::evaluate` is invoked at all, not deferred until the whole pipeline including the evaluator's model-adapter calls has completed.

---

# 8. CLI Behavior

### `llx serve [--port <N>]`
- Starts the `/src/api` HTTP + WebSocket server.
- Binds to `127.0.0.1:<N>`, default **4747**.
- Validates `ANTHROPIC_API_KEY` and port availability.
- Prints: `Listening on http://127.0.0.1:4747`.
- Runs until interrupted, finishing any in‑flight request first.
- Does not appear in `llx explain` or `llx trace` output.

---

# 9. Examples

### `POST /run`
Request:
```json
{ "source": "Load the article from \"article.txt\".\nSummarize it." }
```

Immediate HTTP response:
```json
{ "accepted": true, "correlation_id": "abc-123" }
```

WebSocket event stream:
```json
{ "event_type": "pipeline_started", "correlation_id": "abc-123", ... }
{ "event_type": "final_result_ready", "correlation_id": "abc-123", "data": { "final_result": { "text": "…", "content_type": "plain" } } }
```

### `POST /explain`
Emits:
- `pipeline_started`
- `raw_ast_generated`
- `normalized_ast_generated`

### `POST /trace`
Emits, for the same single-`Summarize`-step source above (N = 1 model-calling operation):
- `pipeline_started`
- `raw_ast_generated`
- `normalized_ast_generated`
- `ir_generated`
- `prompt_generated`
- `model_output_generated`
- `final_result_ready`

A program with more model-calling operations (e.g. `Summarize` → `Translate`) yields one additional `prompt_generated`/`model_output_generated` pair per operation, in program order, still ending in a single `final_result_ready`.

---

# 10. Error Conditions

Every error object includes a fixed `code` and `category` alongside `message`/`severity`/`location`.

### Ack-Phase Errors (synchronous HTTP, no `correlation_id`)

These occur before a request enters the pipeline — no `correlation_id` is allocated and no WebSocket event is emitted. The HTTP response body carries the same error object shape as a streamed error.

| Condition | HTTP status | `code` | `category` | `severity` |
|---|---|---|---|---|
| Malformed JSON request body | `400` | `ERR_MALFORMED_REQUEST` | `api` | `error` |
| Missing `source` field | `400` | `ERR_MISSING_FIELD` | `api` | `error` |

### Pipeline Errors (streamed over WebSocket as `pipeline_failed`)

| Condition | `code` | `category` | `severity` |
|---|---|---|---|
| CNL parse failure | `ERR_CNL_PARSE` | `pipeline` | `error` |
| CNL normalization failure | `ERR_CNL_NORMALIZE` | `pipeline` | `error` |
| IR compilation failure | `ERR_IR_COMPILE` | `pipeline` | `error` |
| Evaluator fatal error | `ERR_EVALUATOR_FATAL` | `pipeline` | `fatal` |
| Model adapter failure | `ERR_MODEL_ADAPTER` | `pipeline` | `fatal` |

### Streaming Error Events
Any pipeline error must emit:

```json
{
  "event_type": "pipeline_failed",
  "correlation_id": "<id>",
  "errors": [ ... ]
}
```

### Server Startup Errors (not events)
- Port already in use → fatal CLI error
- Bind permission denied → fatal CLI error
- `ANTHROPIC_API_KEY` unset → fatal CLI error

---

# 11. BDD Scenarios
Acceptance criteria for this module are defined in **`spec/bdd-api.md`**, using the extended GIVEN/WHEN/THEN/SO THAT/AS MEASURED BY format.

---

# 12. Non‑Goals

`/src/api` does **not** support:
- authentication or authorization
- TLS / HTTPS
- binding to any non-loopback address
- concurrent/parallel request handling
- **single-response mode** (removed)
- request history or persistence
- new grammar, AST nodes, IR nodes, or evaluator behavior
- remote access from another machine

---

# 13. Future Extensions

- Additional observability events (timing, resource usage)
- SSE fallback for environments without WebSocket support
- Token-based authentication if remote access is ever required
- Request queueing for bounded concurrency

---

# Summary
`/src/api` is a thin, deterministic, local-only HTTP + WebSocket wrapper around the existing `run`/`explain`/`trace` pipeline operations.  
It introduces **event-streamed JSON responses** as the sole transport mode, eliminating large final JSON payloads and enabling the `/ui` client to render results incrementally and responsively.