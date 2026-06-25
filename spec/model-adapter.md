# Model Adapter Specification

## Purpose
This document defines the **Claude Model Adapter** used by the Limelight‑X evaluator.  
The model adapter is the only nondeterministic component in the system.  
It provides a deterministic interface for executing model calls while isolating all backend‑specific behavior.

This specification is **authoritative**.  
The evaluator must call the model adapter exactly as defined here.  
The adapter must not infer behavior outside this document.

---

# 1. Overview

The model adapter exposes a single capability:

```
complete(prompt: String) -> Result<String, Error>
```

It sends a prompt to the **Anthropic Claude 3.5 Sonnet** model using the **Messages API**, receives the response, extracts the text, and returns it to the evaluator.

The adapter must be:

- deterministic in configuration  
- stateless  
- synchronous or async depending on evaluator needs  
- fully specified (no hidden defaults)  
- compliant with the coding standards  

---

# 2. Model Configuration

The adapter must use the following fixed configuration:

### 2.1 Model ID
```
claude-3-5-sonnet-20241022
```

### 2.2 API Endpoint
```
POST https://api.anthropic.com/v1/messages
```

### 2.3 API Key Source
The adapter must read the API key from:

```
ANTHROPIC_API_KEY
```

If the variable is missing, the adapter must return a fatal error.

### 2.4 Request Parameters
The adapter must send the following parameters for every request:

```
max_tokens: 2048
temperature: 0.0
system: None
```

These values enforce deterministic behavior.

### 2.5 Transport
The adapter must use:

```
reqwest
```

as the HTTP client.

Async or blocking mode depends on the evaluator implementation, but the API surface must remain identical.

---

# 3. Request Format

The adapter must send the following JSON body:

```
{
  "model": "claude-3-5-sonnet-20241022",
  "max_tokens": 2048,
  "temperature": 0.0,
  "messages": [
    {
      "role": "user",
      "content": "<PROMPT>"
    }
  ]
}
```

Where `<PROMPT>` is the exact string provided by the evaluator.

### Rules

1. The adapter must not modify the prompt.  
2. The adapter must not add system prompts.  
3. The adapter must not add metadata or hidden instructions.  
4. The adapter must not retry unless explicitly instructed by the evaluator (v0.1: no retries).  

---

# 4. Response Format

The adapter must parse the response according to the Anthropic Messages API schema.

The returned text must be extracted from:

```
response.content[0].text
```

If the response does not match this structure, the adapter must return a fatal error.

The adapter must return:

```
Ok(String)
```

containing the extracted text.

---

# 5. Error Semantics

The adapter must return descriptive errors for:

### 5.1 Missing API Key
```
Missing environment variable: ANTHROPIC_API_KEY
```

### 5.2 Network Errors
Any reqwest error must be wrapped in:

```
Error::ModelAdapterNetworkError(String)
```

### 5.3 Invalid JSON
```
Error::ModelAdapterInvalidResponse(String)
```

### 5.4 Missing or malformed fields
```
Error::ModelAdapterMalformedResponse(String)
```

### 5.5 HTTP Errors
Non‑200 responses must produce:

```
Error::ModelAdapterHttpError(status_code, body)
```

All errors must be fatal to the evaluator.

---

# 6. Determinism Requirements

To maintain deterministic evaluation:

1. `temperature` must always be `0.0`.  
2. `max_tokens` must always be `2048`.  
3. No system prompt may be added.  
4. No retries may be performed.  
5. No sampling parameters other than temperature may be set.  
6. The adapter must not modify the prompt.  

---

# 7. Adapter Interface

The adapter must expose the following Rust interface:

```
pub trait ModelAdapter {
    fn complete(&self, prompt: &str) -> Result<String, Error>;
}
```

The evaluator must depend only on this trait.

The concrete implementation must be:

```
ClaudeModelAdapter
```

located in:

```
src/model/claude.rs
```

---

# 8. Example

### Input prompt:
```
Summarize the following text clearly and concisely:

<INPUT>
```

### HTTP request body:
```
{
  "model": "claude-3-5-sonnet-20241022",
  "max_tokens": 2048,
  "temperature": 0.0,
  "messages": [
    {
      "role": "user",
      "content": "Summarize the following text clearly and concisely:\n\n<INPUT>"
    }
  ]
}
```

### Extracted output:
```
"Here is the summary..."
```

---

# 9. Non‑Goals

The model adapter does **not**:

- support multiple models  
- support multiple hosts  
- support streaming  
- support retries  
- support system prompts  
- support temperature > 0  
- support batching  
- support parallel execution  

These may be added in future versions.

---

# Summary

The model adapter is a deterministic wrapper around the Anthropic Claude 3.5 Sonnet Messages API.  
It exposes a single `complete(prompt)` function, sends a fixed‑configuration request, extracts the text response, and returns it to the evaluator.  
All behavior is fully specified and must not be inferred.