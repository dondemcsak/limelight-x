# Limelight‑X Architecture

## Purpose
This document defines the complete architecture of the Limelight‑X expression layer.  
It specifies the pipeline, module boundaries, data flow, and responsibilities of each component.

This specification is authoritative.  
All implementation must follow this architecture exactly.

---

# 1. High‑Level Overview

Limelight‑X is a **single‑language expression layer** that compiles Constrained Natural Language (CNL) into a deterministic Intermediate Representation (IR) and evaluates it using a combination of local logic and a cloud‑based model adapter.

The pipeline is:

```
CNL → Parser → Raw AST → Normalizer → Normalized AST → IR Compiler → IR → Evaluator → Model Adapter → Result
```

Each stage is isolated, deterministic, and spec‑driven.

---

# 2. Module Structure

The repository must follow this structure:

```
/src
    /cli
    /parser
    /normalizer
    /ir
    /evaluator
    /model
    /api
/ui
    /views
    /viewmodels
    /services
    /components
    /styles
    /routing
/spec
    architecture.md
    cnl-grammar.md
    ast-normalizer.md
    ir.md
    evaluator-semantics.md
    model-adapter.md
    api.md
    coding-standards.md
    bdd.md
    bdd-api.md
    spec-template.md
    /ux
        ui-architecture.md
        ui-components.md
        ui-viewmodels.md
        ui-styling-theming.md
        ui-routing-navigation.md
        ui-data-contracts.md
        ui-error-handling.md
        ui-accessibility.md
        ui-build-pipeline.md
        ui-testing.md
        ui-deployment.md
        bdd-ui-interactions.md
        bdd-ui-navigation.md
        bdd-ui-error-cases.md
        bdd-ui-visual-regressions.md
```

### Rules

- Each module has exactly one responsibility.  
- No circular dependencies.  
- No shared mutable state.  
- No “utils” or “helpers” modules.  
- AST definitions live inside `/parser` and `/normalizer` (no `/ast` module).  
- `/src/api` is defined in `spec/api.md`. It orchestrates calls to the existing pipeline stages over local HTTP; it does not introduce new pipeline stages or modify existing ones.  
- `/ui` is a separate Avalonia/.NET (C#) client defined in `spec/ux/*.md`. It is a deliberately-scoped second language; `/src` remains single-language Rust. See CLAUDE.md §1.1.

---

# 3. Pipeline Stages

## 3.1 Parser → Raw AST

The parser:

- tokenizes the CNL input  
- matches grammar patterns  
- constructs a **raw AST**  
- preserves pronouns, names, and implicit inputs  
- does not resolve references  
- does not perform semantic analysis  

The raw AST may contain:

- `Pronoun("it")`  
- `NamedVariable("summary")`  
- `input = None` (implicit input)  
- `Bind` nodes  

The parser is **pure** and **synchronous**.

---

## 3.2 Normalizer → Normalized AST

The normalizer transforms the raw AST into a **canonical, explicit AST**.

Responsibilities:

- resolve pronouns → `PreviousResult`  
- resolve named variables → underlying `InputRef`  
- resolve implicit inputs → `PreviousResult`  
- maintain a symbol table for Bind statements  
- remove Bind nodes from output  
- ensure all inputs are explicit  

The normalized AST contains only:

```
InputRef::PreviousResult
InputRef::Resource(String)
```

The normalized AST contains **no**:

- NamedVariable  
- Pronoun  
- Bind  
- implicit input  

The normalizer is **pure** and **synchronous**.

---

## 3.3 IR Compiler → IR

The IR compiler converts the normalized AST into a **linear execution plan**.

Responsibilities:

- assign `$N` references  
- preserve operation order  
- convert InputRef → IRRef  
- embed custom prompts verbatim  
- produce IR nodes defined in `ir.md`  

The IR is a vector of operations:

```
[
  Load { ... },
  Extract { ... },
  Summarize { ... },
  Translate { ... },
  Rewrite { ... },
  Format { ... }
]
```

The IR compiler is **pure** and **synchronous**.

---

## 3.4 Evaluator → Execution

The evaluator executes the IR in order.

Responsibilities:

1. Resolve `$N` references  
2. Execute built‑in operations (e.g., Load)  
3. Construct deterministic prompts  
4. Call the model adapter  
5. Store results in a vector  
6. Produce the final output  

The evaluator is the only component that performs:

- I/O  
- model calls  
- async operations  

The evaluator must follow the rules in `evaluator-semantics.md`.

---

## 3.5 Model Adapter → Claude API

The model adapter is the only nondeterministic component.

Responsibilities:

- send prompts to Claude 3.5 Sonnet  
- use deterministic request parameters  
- extract text from the response  
- return a `Result<String>`  

The adapter must follow `model-adapter.md`.

---

## 3.6 API Layer → HTTP Wrapper (optional, for `/ui`)

`/src/api` exposes the same `run`/`explain`/`trace` operations over local HTTP, for use by the `/ui` Avalonia client. It sits alongside the CLI, not inside the pipeline:

```
        ┌── CLI (llx run/explain/trace) ──┐
CNL ──► │                                  ├──► Parser → ... → Evaluator → Model Adapter → Result
        └── API (llx serve → /run/explain/trace) ─┘
```

Full details (port, binding, lifecycle, request/response schemas) are defined in `spec/api.md`. The API layer does not reimplement, skip, or reorder any pipeline stage — it calls the same functions the CLI calls.

---

# 4. Data Flow Summary

```
CNL
 ↓
Parser
 ↓
Raw AST
 ↓
Normalizer
 ↓
Normalized AST
 ↓
IR Compiler
 ↓
IR
 ↓
Evaluator
 ↓
Model Adapter
 ↓
Final Result
```

Each stage receives a well‑defined input and produces a well‑defined output.

---

# 5. CLI Integration

The CLI exposes four commands:

### `llx run <file>`
- parses  
- normalizes  
- compiles  
- evaluates  
- prints final result  

### `llx explain <file>`
- parses  
- normalizes  
- compiles  
- prints:
  - raw AST  
  - normalized AST  
  - IR  

### `llx trace <file>`
- parses  
- normalizes  
- compiles  
- evaluates  
- prints:
  - raw AST  
  - normalized AST  
  - IR  
  - constructed prompts  
  - model outputs  
  - final result  

### `llx serve [--port <N>]`
- starts the `/src/api` HTTP server (see `spec/api.md`)
- exposes `/run`, `/explain`, `/trace` over local HTTP for the `/ui` client
- reuses the same pipeline invocations as the equivalent CLI commands

The CLI must not perform evaluation logic.

---

# 6. Determinism Requirements

Limelight‑X must be deterministic except for model output.

### Deterministic components:
- parser  
- normalizer  
- IR compiler  
- evaluator prompt construction  
- evaluator operation order  
- model adapter configuration  

### Nondeterministic component:
- model output only  

All nondeterminism must be isolated to the model adapter.

---

# 7. Error Handling

All fatal errors must include:

- operation index (if applicable)  
- operation type  
- human‑readable message  

Errors may occur in:

- parsing  
- normalization  
- IR compilation  
- evaluation  
- model adapter  

Errors must halt execution immediately.

---

# 8. Non‑Goals

Limelight‑X v0.1 does **not** support:

- multiple constrained languages  
- multiple model hosts  
- provider abstraction  
- streaming  
- batching  
- branching or loops  
- parallel execution  
- caching  
- optimization passes  

These may be added in future versions.

---

# Summary

Limelight‑X is a minimal, deterministic expression layer built around a clean pipeline:

```
Parser → Normalizer → IR Compiler → Evaluator → Model Adapter
```

Each stage is isolated, spec‑driven, and fully deterministic except for model output.  
This architecture provides a transparent, educational reference implementation of an expression layer.