# BDD API Scenarios

## Purpose
This document defines the **Behavior‑Driven Development (BDD)** scenarios for the `/src/api` module (`spec/api.md`).  
These scenarios serve as the **acceptance criteria** for `llx serve` and its `/run`, `/explain`, `/trace` HTTP endpoints, and must be implemented as automated tests using a mock model adapter, per CLAUDE.md §6.

Each scenario follows the same extended BDD structure used in `spec/bdd.md`:

- **GIVEN** — initial context  
- **WHEN** — action taken  
- **THEN** — expected behavior  
- **SO THAT** — purpose or user value  
- **AS MEASURED BY** — objective, testable metric  

These scenarios are authoritative.  
Claude must not modify them unless explicitly instructed.

---

# 1. Server Lifecycle Scenarios

## Scenario: `llx serve` starts and binds to the default port
**GIVEN** `ANTHROPIC_API_KEY` is set and port 4747 is free  
**WHEN** the user runs `llx serve`  
**THEN** the server binds to `127.0.0.1:4747` and prints a startup line  
**SO THAT** the `/ui` client has a predictable default endpoint to connect to  
**AS MEASURED BY** a subsequent `POST http://127.0.0.1:4747/explain` receiving a response

---

## Scenario: `llx serve` respects a custom port
**GIVEN** `ANTHROPIC_API_KEY` is set and port 9001 is free  
**WHEN** the user runs `llx serve --port 9001`  
**THEN** the server binds to `127.0.0.1:9001`  
**SO THAT** users can avoid port conflicts  
**AS MEASURED BY** the startup log line containing `127.0.0.1:9001`

---

## Scenario: Startup fails cleanly when the port is already in use
**GIVEN** another process is already bound to port 4747  
**WHEN** the user runs `llx serve`  
**THEN** the process prints a fatal, human‑readable error and exits without binding  
**SO THAT** users get clear feedback instead of a silent failure  
**AS MEASURED BY** a non‑zero exit code and an error message naming the port

---

## Scenario: Startup fails cleanly when `ANTHROPIC_API_KEY` is unset
**GIVEN** `ANTHROPIC_API_KEY` is not set in the environment  
**WHEN** the user runs `llx serve`  
**THEN** the process prints the same fatal error defined in `model-adapter.md` §5.1 and exits without binding  
**SO THAT** misconfiguration is caught before any request can reach the model adapter  
**AS MEASURED BY** a non‑zero exit code and an error message: “Missing environment variable: ANTHROPIC_API_KEY”

---

# 2. `/run` Endpoint Scenarios

## Scenario: Successful run returns only the final result
**GIVEN** the server is running and the mock model adapter returns a fixed completion  
**WHEN** the client sends `POST /run` with `{ "source": "Load the article from \"article.txt\".\nSummarize it." }`  
**THEN** the HTTP response is `{ "accepted": true, "correlation_id": "<id>" }` with no `data` key, and the WebSocket stream for that `correlation_id` subsequently emits exactly `pipeline_started` followed by `final_result_ready`, whose `data` contains only a `final_result` key  
**SO THAT** the UI's Run workflow can display a result without needing AST/IR detail  
**AS MEASURED BY** (1) the HTTP response body containing `accepted: true` and a `correlation_id` and no `data` key, and (2) the WebSocket event sequence for that `correlation_id` being exactly `["pipeline_started", "final_result_ready"]`, with `final_result_ready.data` containing only a `final_result` key, matching `ui-data-contracts.md` §4 and §9

---

## Scenario: Invalid CNL reports a structured parse error via `pipeline_failed`
**GIVEN** the server is running  
**WHEN** the client sends `POST /run` with malformed CNL source  
**THEN** the HTTP response is still the synchronous accept envelope (`accepted: true`, `correlation_id`) — a CNL parse failure is a *pipeline* error, not an ack-phase error, per `api.md` §10 — and the WebSocket stream for that `correlation_id` emits `pipeline_started` followed by `pipeline_failed`, whose `errors[0]` includes a populated `location`  
**SO THAT** the UI can show inline validation errors at the correct line/column, and the HTTP layer never needs to special-case pipeline-stage failures  
**AS MEASURED BY** the WebSocket event sequence being exactly `["pipeline_started", "pipeline_failed"]`, with `pipeline_failed.errors[0].code == "ERR_CNL_PARSE"` and `.location.line` matching the malformed line in the source

---

## Scenario: Evaluator fatal error halts and reports operation index via `pipeline_failed`
**GIVEN** the server is running and the source references a missing file  
**WHEN** the client sends `POST /run` with `{ "source": "Load the article from \"missing.txt\"." }`  
**THEN** the HTTP response is the synchronous accept envelope, and the WebSocket stream for that `correlation_id` emits `pipeline_started` followed by `pipeline_failed`, whose `errors[0].message` includes the operation index  
**SO THAT** users can trace the failure back to the exact pipeline step even though the failure surfaces asynchronously  
**AS MEASURED BY** the WebSocket event sequence being exactly `["pipeline_started", "pipeline_failed"]`, with `pipeline_failed.errors[0].code == "ERR_EVALUATOR_FATAL"` and `.message` containing both the operation index and “missing.txt”

---

# 3. `/explain` Endpoint Scenarios

## Scenario: Successful explain returns raw and normalized AST only
**GIVEN** the server is running  
**WHEN** the client sends `POST /explain` with valid CNL source  
**THEN** the HTTP response is the accept envelope, and the WebSocket stream for that `correlation_id` emits `pipeline_started` → `raw_ast_generated` → `normalized_ast_generated`, with no model call occurring at any point, and `raw_ast_generated` is received as its own distinct WebSocket frame observably before `normalized_ast_generated` arrives — not merely correctly ordered within a buffered array of collected events  
**SO THAT** the UI's live validation can call `/explain` cheaply without incurring model cost, and so the two AST stages are verified to be genuinely incremental rather than computed-then-flushed together  
**AS MEASURED BY** a test harness that records a separate wall-clock receipt timestamp for each WebSocket frame as it arrives (not after collecting all frames into an array): `raw_ast_generated`'s receipt timestamp must be strictly earlier than `normalized_ast_generated`'s, AND the mock model adapter must show zero invocations at the moment `raw_ast_generated` is received

---

## Scenario: Explain surfaces normalization errors without evaluating
**GIVEN** the server is running and the source contains an unresolvable pronoun (e.g. `Summarize it.` with no prior statement)  
**WHEN** the client sends `POST /explain`  
**THEN** the HTTP response is the accept envelope, and the WebSocket stream for that `correlation_id` emits `pipeline_started` → `raw_ast_generated` → `pipeline_failed` (raw AST parsing succeeds; normalization is what fails), with `pipeline_failed.errors[0].message` matching the normalizer's error message  
**SO THAT** invalid programs are caught before the user attempts to run them, and the UI can still render the raw AST it already received even though normalization failed  
**AS MEASURED BY** the WebSocket event sequence being exactly `["pipeline_started", "raw_ast_generated", "pipeline_failed"]` (never reaching `normalized_ast_generated`), with `pipeline_failed.errors[0].code == "ERR_CNL_NORMALIZE"` and `.message` matching “No prior result for pronoun ‘it’”, per `bdd.md` §2

---

# 4. `/trace` Endpoint Scenarios

## Scenario: Successful trace returns the full pipeline output
**GIVEN** the server is running and the mock model adapter returns a fixed completion  
**WHEN** the client sends `POST /trace` with valid CNL source containing exactly one `Summarize` step  
**THEN** the HTTP response is the accept envelope, and the WebSocket stream for that `correlation_id` emits, in order, `pipeline_started` → `raw_ast_generated` → `normalized_ast_generated` → `ir_generated` → `prompt_generated` → `model_output_generated` → `final_result_ready`, whose `data` payloads collectively contain `raw_ast`, `normalized_ast`, `ir`, `prompt`, `model_output`, and `final_result`  
**SO THAT** the UI's Execution Page can populate all inspector panels as the trace progresses, ending with the final result  
**AS MEASURED BY** the WebSocket event sequence for the `correlation_id` being exactly the 7 events above in order, with each event's `data` containing the correspondingly named key (e.g. `ir_generated.data.ir`, `prompt_generated.data.prompt`, `model_output_generated.data.model_output`), each non‑empty

---

## Scenario: Trace pipeline stage events are emitted incrementally, not batched after the pipeline completes
**GIVEN** the server is running with a mock model adapter configured with an artificial minimum latency (e.g. 100ms) before it returns a completion, and a test harness subscribed to the WebSocket that records a wall-clock receipt timestamp for every event as it arrives  
**WHEN** the client sends `POST /trace` with valid CNL source containing exactly one `Summarize` step  
**THEN** each of `raw_ast_generated`, `normalized_ast_generated`, `ir_generated`, and `prompt_generated` is received well before the pipeline's elapsed time reaches the mock adapter's artificial delay, and `model_output_generated` is received strictly after the mock adapter's recorded invocation; none of these events are delayed until the pipeline as a whole (including the model call) has finished  
**SO THAT** the UI's inspector panels populate progressively as the pipeline runs — giving visible incremental progress instead of a single delayed flush at the end — and so that a regression to “compute everything in one task, then fire all events” is caught automatically rather than silently shipping  
**AS MEASURED BY** all of the following, on a single `POST /trace` call: (1) receipt timestamps of `raw_ast_generated < normalized_ast_generated < ir_generated` are strictly increasing; (2) the gap between `pipeline_started`'s receipt and `ir_generated`'s receipt does not include the mock adapter's configured 100ms delay (proving `ir_generated` did not wait on the model call); (3) the gap between `pipeline_started`'s receipt and `prompt_generated`'s receipt likewise does not include the configured delay (proving `prompt_generated` was sent before the model call, not synthesized from the evaluator's return value afterward — note this is checked via elapsed time against the configured delay, not by comparing directly against the adapter's own recorded invocation instant, since the server-side gap between sending `prompt_generated` and starting that same call is on the order of nanoseconds, too small for a network-delivered receipt timestamp to reliably precede); (4) `model_output_generated`'s receipt timestamp is strictly later than the mock adapter's recorded invocation timestamp and strictly earlier than `final_result_ready`'s (this direction has the full artificial delay as margin, so it is safe to compare directly against the adapter's own timestamp). An implementation that wraps `parser::parse`, `normalizer::normalize`, `ir::compiler::compile`, and `evaluator::evaluate` inside a single task and only emits events after awaiting that task to completion will fail checks (2) and (3), because every stage event — including `ir_generated` and `prompt_generated` — will arrive only after the mock adapter's artificial delay has already elapsed

---

## Scenario: Trace prompts match the evaluator's constructed prompts exactly
**GIVEN** the server is running and the source contains a `Summarize` step with no custom prompt  
**WHEN** the client sends `POST /trace`  
**THEN** the `prompt_generated` event's `data.prompt.prompt_text` matches the built‑in summarization template exactly, per `evaluator-semantics.md`  
**SO THAT** the UI's Prompt inspector shows the true prompt sent to the model, updated as soon as it is known rather than only at the end of the whole trace  
**AS MEASURED BY** byte‑for‑byte equality between the `prompt_generated` event's `data.prompt.prompt_text` and the evaluator's constructed prompt, AND the `prompt_generated` event's receipt timestamp preceding the mock model adapter's invocation timestamp

---

## Scenario: Multi-step trace emits a prompt/model-output pair per model-calling operation, in order
**GIVEN** the server is running with a mock model adapter that records call order and per-call timestamps, and the source is `Load the article from "article.txt".\nSummarize it.\nTranslate it into French.` (one `Load`, one `Summarize`, one `Translate` — two model-calling operations)  
**WHEN** the client sends `POST /trace`  
**THEN** the WebSocket stream for that `correlation_id` emits, after `ir_generated`, two `prompt_generated`/`model_output_generated` pairs before `final_result_ready` — one for the `Summarize` operation (`operation_index` 1) and one for the `Translate` operation (`operation_index` 2) — with the `Translate` pair arriving only after the `Summarize` pair has fully completed  
**SO THAT** chained transformations (e.g. summarize-then-translate) give the UI live visibility into each model call individually, rather than a single opaque batch covering the whole chain  
**AS MEASURED BY** the WebSocket event sequence being exactly `["pipeline_started", "raw_ast_generated", "normalized_ast_generated", "ir_generated", "prompt_generated", "model_output_generated", "prompt_generated", "model_output_generated", "final_result_ready"]`; the first `prompt_generated`/`model_output_generated` pair's `data.prompt.operation_index`/`data.model_output.operation_index` both equal `1` and the second pair's both equal `2`; and the second `prompt_generated`'s receipt timestamp is strictly later than the first `model_output_generated`'s receipt timestamp (proving the second pair does not start until the first model call has fully returned)

---

# 5. Error Response Shape Scenarios

## Scenario: Malformed JSON body returns a structured error (ack-phase)
**GIVEN** the server is running  
**WHEN** the client sends `POST /run` with a body that is not valid JSON  
**THEN** the server responds synchronously with HTTP 400 and the shared envelope with `success: false`; this is an **ack-phase** error per `api.md` §10 — no `correlation_id` is allocated and no WebSocket event is emitted  
**SO THAT** UI error handling can use one consistent error shape for every failure class  
**AS MEASURED BY** the response body matching the envelope schema in `ui-data-contracts.md` §1, with no `correlation_id` field present

---

## Scenario: Missing required field returns a structured error (ack-phase)
**GIVEN** the server is running  
**WHEN** the client sends `POST /run` with `{}` (no `source` field)  
**THEN** the server responds synchronously with HTTP 400 and `errors: [{ message: "Missing required field 'source'" }]`; this is an **ack-phase** error per `api.md` §10 — no `correlation_id` is allocated and no WebSocket event is emitted  
**SO THAT** integration bugs in the UI client are caught immediately with a clear message  
**AS MEASURED BY** the response `errors[0].message` containing the string `'source'`, with no `correlation_id` field present

---

# 6. Determinism Scenarios

## Scenario: Two concurrent requests are handled sequentially
**GIVEN** the server is running with a mock model adapter that records call order and timing  
**WHEN** two `POST /run` requests are sent at the same time  
**THEN** the second request's pipeline execution does not begin until the first has fully completed  
**SO THAT** Limelight‑X's no‑parallelism guarantee (CLAUDE.md §3.3, `architecture.md` §6) holds for the API layer as well as the CLI  
**AS MEASURED BY** (1) the mock adapter's recorded invocation timestamps showing no overlap between the two requests; and (2) the WebSocket event log showing no interleaving between the two requests' `correlation_id`s — every event carrying the first request's `correlation_id` is fully received before any event carrying the second request's `correlation_id` appears, per `api.md` §2.3

---

# Summary
These BDD scenarios define the complete behavioral contract for `/src/api` in Limelight‑X v0.1: server lifecycle via `llx serve`, the `/run`, `/explain`, and `/trace` endpoints, structured error responses, and sequential (non‑parallel) request handling. All automated tests for this module must map directly to these scenarios, using a mock model adapter and never the real Claude API, per CLAUDE.md §6.
