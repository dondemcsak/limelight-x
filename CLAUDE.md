# CLAUDE.md — Rules for Claude Code Generation

## Purpose
This document defines how Claude must behave when generating or modifying code in the Limelight‑X repository.  
It enforces deterministic, spec‑driven development and prevents architectural drift.

Claude must follow this document exactly.  
If any ambiguity exists, Claude must ask for clarification before generating code.

---

# 1. Repository Structure

Claude must assume the following directory layout:

```
/src
    /cli
    /parser
    /normalizer
    /ir
    /evaluator
    /model
/spec
    architecture.md
    cnl-grammar.md
    ast-normalizer.md
    ir.md
    evaluator-semantics.md
    model-adapter.md
    coding-standards.md
    bdd.md
    spec-template.md
```

### Rules

- There is **no `/ast` module**.  
  AST types live inside `/parser` and `/normalizer`.

- There is **no `/providers` layer** in v0.1.  
  The evaluator calls the model adapter directly.

- Claude must never invent new directories or modules.

---

# 2. Pipeline Responsibilities

Claude must follow the exact pipeline defined in `architecture.md`:

```
CNL → Parser → Raw AST → Normalizer → Normalized AST → IR Compiler → IR → Evaluator → Model Adapter → Result
```

### 2.1 Parser
- Implements grammar from `cnl-grammar.md`
- Produces **raw AST**
- May contain:
  - Pronoun nodes  
  - NamedVariable nodes  
  - Bind nodes  
  - Implicit inputs  

### 2.2 Normalizer
- Implements rules from `ast-normalizer.md`
- Produces **normalized AST**
- Must:
  - Resolve pronouns → PreviousResult  
  - Resolve NamedVariable → underlying InputRef  
  - Resolve implicit inputs → PreviousResult  
  - Remove Bind nodes  
- Normalized AST must contain **no NamedVariable nodes**.

### 2.3 IR Compiler
- Implements rules from `ir.md`
- Produces **linear IR**
- Must:
  - Assign `$N` references  
  - Preserve operation order  
  - Embed custom prompts verbatim  
  - Never emit NamedVariable or Bind nodes  

### 2.4 Evaluator
- Implements rules from `evaluator-semantics.md`
- Executes IR deterministically
- Constructs prompts using strict templates
- Calls the model adapter
- Stores results in a vector

### 2.5 Model Adapter
- Implements `model-adapter.md`
- Calls Claude 3.5 Sonnet via Messages API
- Uses deterministic parameters:
  - temperature = 0.0  
  - max_tokens = 2048  
  - no system prompt  
- Extracts text from `response.content[0].text`

---

# 3. Code Generation Rules

Claude must follow these rules when generating code:

### 3.1 Specs Are Authoritative
Claude must treat all files in `/spec` as the source of truth.

If code conflicts with a spec:
- Claude must update the code, **not** the spec.
- If the spec is ambiguous, Claude must ask for clarification.

### 3.2 No Hidden Behavior
Claude must not:
- add new features  
- add new grammar  
- add new IR nodes  
- add new evaluator behavior  
- add new CLI flags  
- add new model parameters  

Unless explicitly instructed.

### 3.3 Determinism
Claude must ensure:
- no randomness  
- no temperature > 0  
- no retries  
- no nondeterministic ordering  
- no parallel execution  

### 3.4 Error Handling
All errors must:
- be explicit  
- include operation index (if applicable)  
- include human‑readable messages  
- halt execution immediately  

### 3.5 No External Dependencies
Claude must not introduce:
- new crates  
- new libraries  
- new runtime dependencies  

Unless explicitly approved.

---

# 4. File‑Level Rules

### 4.1 Parser (`/src/parser`)
- Must match grammar exactly  
- Must not resolve references  
- Must not perform semantic analysis  

### 4.2 Normalizer (`/src/normalizer`)
- Must implement symbol table  
- Must fully resolve NamedVariable  
- Must remove Bind nodes  
- Must output only explicit InputRef values  

### 4.3 IR Compiler (`/src/ir`)
- Must assign `$N` references  
- Must preserve order  
- Must embed prompts verbatim  

### 4.4 Evaluator (`/src/evaluator`)
- Must follow prompt templates exactly  
- Must call model adapter exactly once per IR op  
- Must not modify prompts  
- Must not add system prompts  

### 4.5 Model Adapter (`/src/model`)
- Must use:
  - model = `claude-3-5-sonnet-20241022`
  - endpoint = `https://api.anthropic.com/v1/messages`
  - API key from `ANTHROPIC_API_KEY`
  - temperature = 0.0  
  - max_tokens = 2048  
- Must extract text from:
  `response.content[0].text`

---

# 5. CLI Rules

Claude must implement:

### `llx run <file>`
- parse → normalize → compile → evaluate → print result

### `llx explain <file>`
- parse → normalize → compile  
- print:
  - raw AST  
  - normalized AST  
  - IR  

### `llx trace <file>`
- parse → normalize → compile → evaluate  
- print:
  - raw AST  
  - normalized AST  
  - IR  
  - prompts  
  - model outputs  
  - final result  

Claude must not add new CLI commands.

---

# 6. Testing Rules

Claude must implement tests that map directly to `bdd.md`.

### Rules
- One test per scenario  
- No additional tests unless requested  
- Tests must be deterministic  
- Tests must not call the real model adapter  
- Tests must use a mock adapter  

---

# 7. Prohibited Behavior

Claude must not:

- invent new architecture  
- introduce providers  
- introduce multiple languages  
- introduce multiple model hosts  
- introduce streaming  
- introduce batching  
- introduce parallelism  
- introduce caching  
- introduce optimization passes  

These are explicitly out of scope for v0.1.

---

# 8. When Claude Must Ask for Clarification

Claude must ask for clarification if:

- a spec is ambiguous  
- two specs conflict  
- a behavior is undefined  
- a new feature is implied but not specified  
- a change would affect multiple modules  

Claude must never guess.

---

# Summary

Claude must implement Limelight‑X exactly as defined in the `/spec` directory.  
The architecture is fixed, deterministic, and single‑language.  
Claude must generate code that is consistent, spec‑driven, and free of hidden behavior.