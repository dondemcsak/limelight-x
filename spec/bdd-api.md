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
**THEN** the response envelope has `success: true` and `data.final_result`  
**SO THAT** the UI's Run workflow can display a result without needing AST/IR detail  
**AS MEASURED BY** the response `data` object containing only a `final_result` key, matching `ui-data-contracts.md` §4

---

## Scenario: Invalid CNL returns a structured parse error
**GIVEN** the server is running  
**WHEN** the client sends `POST /run` with malformed CNL source  
**THEN** the response envelope has `success: false` with a populated `errors[]` entry including `location`  
**SO THAT** the UI can show inline validation errors at the correct line/column  
**AS MEASURED BY** `errors[0].location.line` matching the malformed line in the source

---

## Scenario: Evaluator fatal error halts and reports operation index
**GIVEN** the server is running and the source references a missing file  
**WHEN** the client sends `POST /run` with `{ "source": "Load the article from \"missing.txt\"." }`  
**THEN** the response envelope has `success: false` with an `errors[]` entry whose message includes the operation index  
**SO THAT** users can trace the failure back to the exact pipeline step  
**AS MEASURED BY** `errors[0].message` containing both the operation index and “missing.txt”

---

# 3. `/explain` Endpoint Scenarios

## Scenario: Successful explain returns raw and normalized AST only
**GIVEN** the server is running  
**WHEN** the client sends `POST /explain` with valid CNL source  
**THEN** the response `data` contains `raw_ast` and `normalized_ast`, and no model call occurs  
**SO THAT** the UI's live validation can call `/explain` cheaply without incurring model cost  
**AS MEASURED BY** the mock model adapter recording zero invocations for this request

---

## Scenario: Explain surfaces normalization errors without evaluating
**GIVEN** the server is running and the source contains an unresolvable pronoun (e.g. `Summarize it.` with no prior statement)  
**WHEN** the client sends `POST /explain`  
**THEN** the response envelope has `success: false` with the normalizer's error message  
**SO THAT** invalid programs are caught before the user attempts to run them  
**AS MEASURED BY** `errors[0].message` matching “No prior result for pronoun ‘it’”, per `bdd.md` §2

---

# 4. `/trace` Endpoint Scenarios

## Scenario: Successful trace returns the full pipeline output
**GIVEN** the server is running and the mock model adapter returns a fixed completion  
**WHEN** the client sends `POST /trace` with valid CNL source  
**THEN** the response `data` contains `raw_ast`, `normalized_ast`, `ir`, `prompts`, and `model_outputs` together  
**SO THAT** the UI's Execution Page can populate all inspector panels from a single request  
**AS MEASURED BY** all five keys present in `data` and non‑empty

---

## Scenario: Trace prompts match the evaluator's constructed prompts exactly
**GIVEN** the server is running and the source contains a `Summarize` step with no custom prompt  
**WHEN** the client sends `POST /trace`  
**THEN** `data.prompts[0].prompt_text` matches the built‑in summarization template exactly, per `evaluator-semantics.md`  
**SO THAT** the UI's Prompt inspector shows the true prompt sent to the model  
**AS MEASURED BY** byte‑for‑byte equality between `prompt_text` and the evaluator's constructed prompt

---

# 5. Error Response Shape Scenarios

## Scenario: Malformed JSON body returns a structured error
**GIVEN** the server is running  
**WHEN** the client sends `POST /run` with a body that is not valid JSON  
**THEN** the server responds with HTTP 400 and the shared envelope with `success: false`  
**SO THAT** UI error handling can use one consistent error shape for every failure class  
**AS MEASURED BY** the response body matching the envelope schema in `ui-data-contracts.md` §1

---

## Scenario: Missing required field returns a structured error
**GIVEN** the server is running  
**WHEN** the client sends `POST /run` with `{}` (no `source` field)  
**THEN** the server responds with HTTP 400 and `errors: [{ message: "Missing required field 'source'" }]`  
**SO THAT** integration bugs in the UI client are caught immediately with a clear message  
**AS MEASURED BY** the response `errors[0].message` containing the string `'source'`

---

# 6. Determinism Scenarios

## Scenario: Two concurrent requests are handled sequentially
**GIVEN** the server is running with a mock model adapter that records call order and timing  
**WHEN** two `POST /run` requests are sent at the same time  
**THEN** the second request's pipeline execution does not begin until the first has fully completed  
**SO THAT** Limelight‑X's no‑parallelism guarantee (CLAUDE.md §3.3, `architecture.md` §6) holds for the API layer as well as the CLI  
**AS MEASURED BY** the mock adapter's recorded invocation timestamps showing no overlap between the two requests

---

# Summary
These BDD scenarios define the complete behavioral contract for `/src/api` in Limelight‑X v0.1: server lifecycle via `llx serve`, the `/run`, `/explain`, and `/trace` endpoints, structured error responses, and sequential (non‑parallel) request handling. All automated tests for this module must map directly to these scenarios, using a mock model adapter and never the real Claude API, per CLAUDE.md §6.
