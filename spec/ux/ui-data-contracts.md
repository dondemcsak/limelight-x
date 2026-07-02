# UI Data Contracts

## Purpose
This document defines all backend → UI data contracts used by the Limelight‑X Avalonia workflow dashboard.  
It specifies the JSON schemas, versioning rules, metadata structures, and error formats for all API responses.  
This specification is authoritative for how the UI consumes these contracts.

The server that produces these responses (`/src/api`, its endpoints, port, lifecycle, and startup error conditions) is owned by **`spec/api.md`**; this document owns the JSON shapes and UI-side consumption rules, and must stay in sync with `spec/api.md` §9–10. If the two ever disagree, `spec/api.md` wins for wire format and error semantics.

All implementation must follow these contracts exactly.

Limelight‑X uses:

- **Flat JSON responses**  
- **Shared response envelope**  
- **Versioned namespaces (`v1.*`)**  
- **Rich metadata**  
- **Full semantic AST nodes**  
- **Full IR operation structures**  
- **Prompt blocks with metadata**  
- **Model outputs with parsed models + metadata**  
- **Final result with content type**  
- **Structured error objects**  
- **Forward‑compatible schemas (UI ignores unknown fields)**  

---

# 1. Shared Response Envelope

All backend responses follow the same envelope:

```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "data": { ... }
}
```

### Fields

| Field     | Type     | Description |
|-----------|----------|-------------|
| `version` | string   | API version namespace (`v1`) |
| `success` | boolean  | Indicates whether the request succeeded |
| `errors`  | array    | List of structured error objects |
| `data`    | object   | Endpoint‑specific payload |

### Error Object Schema

```json
{
  "code": "string",
  "category": "validation | pipeline | api | rendering | navigation | editor | state",
  "message": "string",
  "severity": "info | warning | error | fatal",
  "location": {
    "line": "number",
    "column": "number",
    "span": { "start": "number", "end": "number" }
  }
}
```

`code` and `category` map directly onto the UI's `UiError.Code`/`UiError.Category` (`ui-error-handling.md` §1, `ui-viewmodels.md` §2) with no client-side derivation. The full `code`/`category` value list per error condition is defined in `api.md` §10 — that table is authoritative; this schema only describes the field shapes.

### Rules
- UI must ignore unknown fields.  
- `success = false` → `data` may be omitted.  
- `errors` may contain multiple structured errors.  
- `location` is optional.  
- `severity` and `category` strings are deserialized **case-insensitively** into their respective .NET enums (e.g. wire `"fatal"` → `ErrorSeverity.Fatal`).

### HTTP Status Codes
The JSON envelope shape is identical regardless of HTTP status — even a syntactically malformed request body must still receive a standard envelope response, per `api.md` §10 — but the status itself is meaningful. In short: malformed requests (bad JSON, missing required fields) return HTTP 400; pipeline-level failures (parse errors, evaluator fatal errors) return HTTP 200 with `success: false`. `PipelineService` should branch on `success` for pipeline-level results, and treat any non-2xx status as a transport-level `ApiError` using the envelope's `errors[]` for the message.

---

# 2. `/explain` Response Schema

### Purpose
Returns raw AST and normalized AST.

### JSON Schema

```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "data": {
    "raw_ast": {
      "root": { "$ref": "#/definitions/ast_node" },
      "raw_text": "string",
      "metadata": { "$ref": "#/definitions/ast_metadata" }
    },
    "normalized_ast": {
      "root": { "$ref": "#/definitions/ast_node" },
      "raw_text": "string",
      "metadata": { "$ref": "#/definitions/normalized_ast_metadata" }
    }
  }
}
```

---

# 3. `/trace` Response Schema

### Purpose
Returns IR, prompts, model outputs, and optionally ASTs.

### JSON Schema

```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "data": {
    "raw_ast": { "$ref": "#/definitions/raw_ast" },
    "normalized_ast": { "$ref": "#/definitions/normalized_ast" },
    "ir": {
      "operations": [
        { "$ref": "#/definitions/ir_operation" }
      ],
      "raw_text": "string",
      "metadata": { "$ref": "#/definitions/ir_metadata" }
    },
    "prompts": [
      { "$ref": "#/definitions/prompt_block" }
    ],
    "model_outputs": [
      { "$ref": "#/definitions/model_output_block" }
    ]
  }
}
```

---

# 4. `/run` Response Schema

### Purpose
Returns only the final result.

### JSON Schema

```json
{
  "version": "v1",
  "success": true,
  "errors": [],
  "data": {
    "final_result": {
      "text": "string",
      "content_type": "plain | markdown | json"
    }
  }
}
```

---

# 5. Definitions

All shared structures are defined under `#/definitions`.

---

## 5.1 AST Node

Full semantic AST node.

```json
{
  "type": "string",
  "value": "string",
  "children": [
    { "$ref": "#/definitions/ast_node" }
  ],
  "span": {
    "start": "number",
    "end": "number"
  },
  "depth": "number",
  "metadata": {
    "resource": "string",
    "pronoun": "string",
    "expression_hole": "boolean",
    "normalized": "boolean"
  }
}
```

---

## 5.2 AST Metadata

```json
{
  "node_count": "number",
  "max_depth": "number",
  "source_length": "number"
}
```

---

## 5.3 Normalized AST Metadata

Includes structural + metadata differences.

```json
{
  "node_count": "number",
  "max_depth": "number",
  "normalization_steps": "number",
  "removed_named_variables": "number",
  "added_input_refs": "number"
}
```

---

## 5.4 IR Operation

Full IR operation structure.

```json
{
  "operation_index": "number",
  "type": "string",
  "input": "number",
  "prompt": "string",
  "target": "string",
  "source_span": {
    "start": "number",
    "end": "number"
  },
  "normalized_source": "string",
  "debug_info": {
    "token_count": "number",
    "estimated_cost": "number"
  }
}
```

---

## 5.5 IR Metadata

```json
{
  "operation_count": "number",
  "max_depth": "number",
  "reference_map": {
    "string": "number"
  }
}
```

---

## 5.6 Prompt Block

Prompt text + index + metadata.

```json
{
  "operation_index": "number",
  "prompt_text": "string",
  "metadata": {
    "length": "number",
    "token_count": "number"
  }
}
```

---

## 5.7 Model Output Block

Full model output structure.

```json
{
  "operation_index": "number",
  "raw_text": "string",
  "content_type": "plain | markdown | json",
  "parsed": {
    "markdown": "object",
    "json": "object"
  },
  "metadata": {
    "token_usage": "number",
    "latency_ms": "number"
  }
}
```

---

## 5.8 Final Result

Raw text + content type.

```json
{
  "text": "string",
  "content_type": "plain | markdown | json"
}
```

---

# 6. Versioning

Limelight‑X uses **versioned namespaces**:

- `v1.run_result`
- `v1.explain_result`
- `v1.trace_result`

### Rules
- All responses include `"version": "v1"`.  
- Future versions may introduce `v2`, `v3`, etc.  
- UI must ignore unknown fields.  
- UI must not assume backward compatibility beyond v1.

---

# 7. Extensibility Rules

### Allowed
- Backend may add new fields.  
- Backend may add new metadata sections.  
- Backend may add new optional structures.

### Forbidden
- Backend may not remove required fields.  
- Backend may not change field types.  
- Backend may not change semantic meaning of fields.

---

# 8. Non‑Goals

Data contracts do **not** include:

- Component input/output contracts  
- UI internal ViewModel structures  
- Binary data (embeddings, images)  
- Plugin contracts  
- Multi‑file project schemas  

---

# Summary

This document defines all backend → UI data contracts for Limelight‑X.  
All responses use a shared envelope, versioned namespaces, rich metadata, full semantic AST nodes, full IR operations, prompt blocks with metadata, model outputs with parsed models, and final results with content types.  
The UI must ignore unknown fields and treat these schemas as authoritative for v0.1.