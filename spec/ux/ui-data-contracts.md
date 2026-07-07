# UI Data Contracts (Streaming Edition)

## Purpose
This document defines all data contracts exchanged between the Limelight‑X UI and the `/src/api` backend.  
It specifies the JSON envelope shape, event types, inspector payloads, error structures, and correlation‑ID rules used in the streaming API.

This specification is authoritative.  
All implementation must follow this contract exactly.

The UI receives **incremental JSON events** over WebSocket instead of single-response HTTP payloads.  
Each event updates the corresponding inspector ViewModel deterministically.

---

# 1. Shared Event Envelope

Every event sent by the backend uses the same envelope shape:

```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "event_type": "raw_ast_generated",
  "correlation_id": "abc-123",
  "data": { ... }
}
```

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `version` | string | API version (`"v1"`) |
| `success` | boolean | `true` unless `pipeline_failed` |
| `errors` | array | List of error objects (empty unless error) |
| `event_type` | string | One of the event types defined in §2 |
| `correlation_id` | string | Unique ID for the pipeline execution |
| `data` | object | Event-specific payload |

### Determinism Rules
- Envelope shape is identical for all events.  
- Event order is deterministic and matches pipeline order.  
- UI must ignore events whose `correlation_id` does not match the active execution.  
- UI must not reorder or buffer events.

---

# 2. Event Types

The backend emits the following event types:

| Event Type | Description |
|------------|-------------|
| `pipeline_started` | Pipeline execution has begun; UI must clear all inspectors |
| `raw_ast_generated` | Raw AST is ready |
| `normalized_ast_generated` | Normalized AST is ready |
| `ir_generated` | IR is ready |
| `prompt_generated` | A prompt has been constructed for one model-adapter call |
| `model_output_generated` | A model-adapter call has returned its output |
| `final_result_ready` | Final result is ready |
| `pipeline_failed` | Pipeline encountered an error |

`prompt_generated` and `model_output_generated` are emitted once **per model-calling IR operation**, not once per pipeline run — see the `/trace` sequence below.

### Per-Operation Event Sequences

#### `/run`
```
pipeline_started
final_result_ready
```

Note: in the current UI shell, no button, shortcut, or menu item calls `/run` — the **Run** button invokes `/trace` (§2 below), and `/run` remains a valid, unchanged backend endpoint simply not wired to any UI control (see `ui-viewmodels.md` §6). This is a UI wiring choice only; the endpoint itself and its contract are unchanged.

#### `/explain`
```
pipeline_started
raw_ast_generated
normalized_ast_generated
```

(`/explain` never invokes the evaluator, so it produces no final result; `normalized_ast_generated` is the completion signal for this endpoint.)

#### `/trace`
```
pipeline_started
raw_ast_generated
normalized_ast_generated
ir_generated
( prompt_generated
  model_output_generated ) × N
final_result_ready
```

where N is the number of model-calling IR operations in the program (0 or more), each pair emitted in program order before the next pair begins.

---

# 3. Error Object Contract

Errors follow the same structure defined in `api.md`:

```json
{
  "code": "ERR_CNL_PARSE",
  "category": "pipeline",
  "severity": "error",
  "message": "Unexpected token at line 3",
  "location": {
    "line": 3,
    "column": 14
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `code` | string | Stable error code |
| `category` | string | `api` or `pipeline` |
| `severity` | string | `error` or `fatal` |
| `message` | string | Human-readable message |
| `location` | object or null | Optional parser/normalizer location |

`category` is `api` or `pipeline` only at the wire level — these are the only values a server-sent error object ever carries. `transport` and `ui` (see `ui-error-handling.md` §4) are categories the client synthesizes itself (e.g. for a WebSocket disconnect or a rendering failure) and never appear in an error object received from the server.

### Rules
- All errors must be human-readable.  
- `pipeline_failed` events must include at least one error.  
- UI must surface errors immediately.

---

# 4. AST Node Contract (Raw AST)

Used in `raw_ast_generated` events.

```json
{
  "data": {
    "raw_ast": [
      {
        "node_type": "Command",
        "text": "Load the article from \"article.txt\".",
        "children": [ ... ],
        "span": { "start": 0, "end": 42 }
      }
    ]
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `node_type` | string | AST node type |
| `text` | string | Original text |
| `children` | array | Child AST nodes |
| `span.start` | number | Start offset |
| `span.end` | number | End offset |

---

# 5. Normalized AST Contract

Used in `normalized_ast_generated` events.

```json
{
  "data": {
    "normalized_ast": [
      {
        "node_type": "LoadResource",
        "resource": "article.txt",
        "children": []
      }
    ]
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `node_type` | string | Normalized AST node type |
| `resource` | string or null | Resource name (if applicable) |
| `children` | array | Child normalized nodes |

---

# 6. IR Operation Contract

Used in `ir_generated` events.

```json
{
  "data": {
    "ir": [
      {
        "operation_type": "LoadResource",
        "resource": "article.txt",
        "index": 0,
        "metadata": { "source_span": { "start": 0, "end": 42 } }
      }
    ]
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `operation_type` | string | IR operation type |
| `resource` | string or null | Resource name |
| `index` | number | Operation index |
| `metadata` | object | Additional deterministic metadata |

---

# 7. Prompt Block Contract

Used in `prompt_generated` events. Each event covers exactly **one** model-calling IR operation — `data.prompt` is a single object, not an array.

```json
{
  "data": {
    "prompt": {
      "operation_index": 1,
      "prompt_text": "Summarize the article.",
      "metadata": {
        "length": 24,
        "token_count": 4
      }
    }
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `operation_index` | number | IR operation index |
| `prompt_text` | string | Prompt sent to the model |
| `metadata` | object | Model adapter metadata |

---

# 8. Model Output Contract

Used in `model_output_generated` events. Each event covers exactly **one** model-calling IR operation — `data.model_output` is a single object, not an array.

```json
{
  "data": {
    "model_output": {
      "operation_index": 1,
      "raw_text": "Here is the summary...",
      "content_type": "plain",
      "metadata": {
        "token_usage": 128,
        "latency_ms": 842
      }
    }
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `operation_index` | number | IR operation index |
| `raw_text` | string | Model output text |
| `content_type` | string | `"plain"` or `"json"` |
| `metadata` | object | Model adapter metadata |

---

# 9. Final Result Contract

Used in `final_result_ready` events.

```json
{
  "data": {
    "final_result": {
      "text": "The article discusses...",
      "content_type": "plain"
    }
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `text` | string | Final result text |
| `content_type` | string | `"plain"` or `"json"` |

---

# 10. Correlation ID Contract

Every pipeline execution is associated with a unique `correlation_id`.

### Rules
- UI must track the active `correlation_id`.  
- UI must ignore events with mismatched IDs.  
- UI must clear all inspector state when a new `pipeline_started` event arrives.  
- UI must not buffer or reorder events.

---

# 11. WebSocket Event Stream Contract

### Endpoint
```
ws://127.0.0.1:<port>/events
```

### Transport Rules
- Events must be UTF‑8 JSON objects.  
- Events must not be chunked or batched.  
- Each event must be a complete JSON object.  
- No partial frames.  
- No binary frames.  
- No compression.

---

# 12. Non‑Goals

Data contracts do **not** support:

- single-response HTTP mode  
- parallel pipeline executions  
- queued executions  
- cancellation  
- custom inspectors  
- nondeterministic fields  
- UI-side pipeline reconstruction  

---

# 13. Future Extensions

Potential enhancements:

- additional observability events (timing, resource usage)  
- richer metadata for IR and AST nodes  
- structured model output diffing  
- SSE fallback transport  

---

# Summary

The Limelight‑X UI data contracts define a deterministic, streaming JSON event model used to update inspector ViewModels in real time.  
All pipeline results arrive incrementally over WebSocket, each wrapped in a stable envelope with a correlation ID and event type.  
This contract ensures predictable, spec‑driven behavior across the entire UI.