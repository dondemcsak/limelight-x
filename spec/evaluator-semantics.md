# Evaluator Semantics

## Purpose
This document defines the deterministic execution semantics of the Limelight‑X evaluator.  
The evaluator consumes the IR produced by the IR compiler and executes each operation in order, producing a final result.

The evaluator is the only component that performs:
- I/O  
- model calls  
- async operations  

All behavior in this document is **authoritative**.  
The evaluator must not infer or modify behavior outside this specification.

---

# 1. Overview

The evaluator executes a **linear sequence of IR operations**:

```
IR[0], IR[1], IR[2], ...
```

For each operation:

1. Resolve all `$N` references  
2. Execute the operation  
3. Store the result in `results[N]`  
4. Continue to the next operation  

The evaluator must be:

- deterministic  
- side‑effect‑free except for file reads and model calls  
- transparent (traceable)  
- spec‑driven  

---

# 2. Execution Model

The evaluator maintains:

```
results: Vec<String>
```

Each IR operation appends a new string to this vector.

### Reference Resolution

```
"$0" → results[0]
"$1" → results[1]
...
```

Rules:

- `$N` must reference an earlier index  
- `$N` must exist  
- `$N` must not reference future operations  

Violations produce fatal errors.

---

# 3. Operation Semantics

The evaluator supports six IR operations:

- Load  
- Extract  
- Summarize  
- Translate  
- Rewrite  
- Format  

Each operation is defined below.

---

# 3.1 Load

```
IR::Load { path }
```

### Behavior
1. Read the file at `path` into a string.  
2. Store the contents in `results[N]`.

### Errors
- File not found  
- Permission denied  
- Non‑UTF‑8 content  

### Example
```
Load { path: "article.txt" }
→ results[N] = "<file contents>"
```

---

# 3.2 Extract

```
IR::Extract { target, input }
```

### Behavior
1. Resolve `input` → `text`.  
2. Construct the built‑in extraction prompt:

```
Extract the following <target> from the text:

<text>
```

3. Call the model adapter with the constructed prompt.  
4. Store the model output in `results[N]`.

### Errors
- Invalid reference  
- Model adapter error  

---

# 3.3 Summarize

```
IR::Summarize { input, prompt }
```

### Behavior

1. Resolve `input` → `text`.  
2. Construct the prompt:

### Case A — Built‑in prompt (`prompt = None`)
```
Summarize the following text clearly and concisely:

<text>
```

### Case B — Custom prompt (`prompt = Some(p)`)
```
<p>

Here is the text to summarize:

<text>
```

3. Call the model adapter.  
4. Store the output in `results[N]`.

### Errors
- Invalid reference  
- Model adapter error  

---

# 3.4 Translate

```
IR::Translate { input, language, prompt }
```

### Behavior

1. Resolve `input` → `text`.  
2. Construct the prompt:

### Case A — Built‑in prompt
```
Translate the following text into <language>:

<text>
```

### Case B — Custom prompt
```
<p>

Here is the text to translate into <language>:

<text>
```

3. Call the model adapter.  
4. Store the output.

---

# 3.5 Rewrite

```
IR::Rewrite { input, prompt }
```

### Behavior

1. Resolve `input` → `text`.  
2. Construct the prompt:

### Case A — Built‑in prompt
```
Rewrite the following text to improve clarity and readability:

<text>
```

### Case B — Custom prompt
```
<p>

Here is the text to rewrite:

<text>
```

3. Call the model adapter.  
4. Store the output.

---

# 3.6 Format

```
IR::Format { input, target }
```

### Behavior

1. Resolve `input` → `text`.

### Case A — target = "JSON"

2. Construct the tabular‑format prompt:

```
Convert the following text into a pipe-delimited Markdown table with a header row:

<text>
```

3. Call the model adapter with the tabular‑format prompt.  
4. Parse the model's pipe‑delimited table into a JSON array of objects:
   - Treat the first non‑separator row as column headers.  
   - Skip the separator row (the row of `-` and `|` characters).  
   - Strip Markdown inline formatting (`**`, `__`, `*`, `_`) from every cell value.  
   - Each subsequent data row becomes a JSON object with keys taken from the header row.  
5. Store the resulting JSON array string in `results[N]`.

### Case B — other targets

2. Construct the prompt:

```
Format the following text as <target>:

<text>
```

3. Call the model adapter.  
4. Store the output in `results[N]`.

### Errors
- Invalid reference  
- Model adapter error  
- (Case A only) Model output is not a valid pipe‑delimited table: fatal error with operation index

---

# 4. Model Adapter Integration

The evaluator must call:

```
adapter.complete(prompt) -> Result<String, Error>
```

The evaluator must not:

- modify the prompt  
- add system prompts  
- retry automatically  
- change temperature or max_tokens  

All model behavior is defined in `model-adapter.md`.

---

# 5. Trace Mode

When running in trace mode, the evaluator must print:

1. Raw AST  
2. Normalized AST  
3. IR  
4. For each IR operation:
   - operation index  
   - operation type  
   - resolved inputs  
   - constructed prompt  
   - model output (if applicable)  
5. Final result  

Trace output must be human‑readable.

---

# 6. Explain Mode

Explain mode prints:

- raw AST  
- normalized AST  
- IR  

It must **not** evaluate the IR or call the model adapter.

---

# 7. Error Semantics

All fatal errors must include:

- operation index  
- operation type  
- human‑readable message  

### Error Types

- Invalid IR reference  
- Missing previous result  
- File read error  
- Model adapter error  
- Malformed IR  
- Unexpected evaluator state  

Errors must halt evaluation immediately.

---

# 8. Determinism Requirements

The evaluator must be deterministic except for model output.

Deterministic components:

- reference resolution  
- prompt construction  
- operation ordering  
- file reading  
- error handling  

Nondeterministic component:

- model output only  

---

# 9. Non‑Goals

The evaluator does **not**:

- perform optimization  
- perform static analysis  
- support branching or loops  
- support multiple model hosts  
- support streaming  
- support batching  
- support parallel execution  

These may be added in future versions.

---

# Summary

The evaluator executes IR operations deterministically, constructs prompts according to strict templates, delegates model calls to the model adapter, and produces a final result.  
Trace and explain modes provide full transparency into the pipeline.