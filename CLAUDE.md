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
    /api
/ui
    /views
    /viewmodels
    /services
    /components
    /styles
    /routing
    /intellisense
    /native
    /queries
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
    cnl-editor-architecture.md
    /parsing
        peg-grammar.md
        grammer-js.md
        highlights-scm.md
        folds-scm.md
        injections-scm.md
        tree-sitter-integration.md
        tree-sitter-build-guide.md
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
        ui-editor-services-guide.md
        ui-intellisense-architecture.md
        ui-intellisense-engine-spec.md
        ui-intellisense-implementation-guide.md
        bdd-ui-interactions.md
        bdd-ui-navigation.md
        bdd-ui-error-cases.md
        bdd-ui-visual-regressions.md
```

### Rules

- There is **no `/ast` module**.  
  AST types live inside `/parser` and `/normalizer`.

- There is **no `/providers` layer** in v0.1.  
  The evaluator calls the model adapter directly.

- `/src/api` is the sole exception to the "no new modules" rule: it wraps the existing `run`/`explain`/`trace` pipeline logic behind a local HTTP interface, as defined in `spec/api.md`. It does not reimplement or bypass any pipeline stage.

- `/ui/intellisense` holds the Tree‑sitter‑backed editor services (`ParserHost`, `QueryRunner`, `CompletionService`, `DiagnosticService`, `HoverService`, `FoldingService`, `OutlineService`) defined in `spec/ux/ui-editor-services-guide.md` and `spec/ux/ui-intellisense-*.md`. `/ui/native` and `/ui/queries` are asset‑only companions (no code) holding, respectively, the compiled `tree-sitter-limelightx.dll` and its `.scm` query files — see `spec/cnl-editor-architecture.md` and `spec/parsing/tree-sitter-integration.md`.

- Claude must never invent new directories or modules beyond what is listed here without explicit instruction.

### 1.1 Multi‑Component Scope

Limelight‑X now has two components governed by different rules:

- **`/src` (the core pipeline, including `/src/api`)** — governed by this document in full: single‑language (Rust), deterministic, no providers, no new modules beyond `/src/api`.
- **`/ui`** — a separate Avalonia/.NET (C#) desktop client, governed by `spec/ux/*.md`. It is a second language deliberately scoped to this one boundary and does not violate `/src`'s single‑language or determinism rules. `/ui` must not reimplement pipeline stages; it may only call `/src/api`'s HTTP endpoints.

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

**Explicitly approved for `/src/api`:** an HTTP server crate (e.g. `axum` or `actix-web`) sufficient to implement `spec/api.md`. No other new Rust crates are approved.

**Explicitly approved for `/ui`:** Avalonia, Avalonia Community Toolkit, a Fluent UI icon set, Inter and JetBrains Mono fonts, MSIX packaging tooling, and — for persistent diagnostic logging — `Microsoft.Extensions.Logging` plus Serilog (`Serilog.Extensions.Logging`, `Serilog.Sinks.File`), per `spec/ux/*.md`. These apply only to `/ui` and do not license any further additions without explicit approval.

**Also explicitly approved for `/ui`:** a native Tree‑sitter grammar library, `tree-sitter-limelightx.dll` (built from `tree-sitter/grammar.js` per `spec/parsing/tree-sitter-build-guide.md`), loaded via hand‑written, raw `[DllImport]` P/Invoke bindings only — **no third‑party Tree‑sitter binding NuGet package (e.g. TreeSitterSharp) is approved.** The DLL is currently built for **ARM64 only**; a `win-x64` build (matching `ui-build-pipeline.md` §7.1's pinned `RuntimeIdentifier`) is explicitly deferred future work. Until the `win-x64` build exists, any test or code path that loads this DLL must be skippable/gated on the `windows-latest` (x64) CI runner. This entry governs `/ui/native`, `/ui/queries`, and `/ui/intellisense` (see §1) and does not license any further native/interop additions without explicit approval.

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

### `llx serve [--port <N>]`
- starts the `/src/api` HTTP server defined in `spec/api.md`
- binds to `127.0.0.1` on the given port (default defined in `spec/api.md`)
- fails fast if `ANTHROPIC_API_KEY` is unset or the port is unavailable
- serves `/run`, `/explain`, `/trace` by invoking the same pipeline logic as the equivalent CLI commands, one request at a time
- runs until interrupted (Ctrl+C), then shuts down cleanly

Claude must not add CLI commands beyond these four without explicit instruction.

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
- introduce multiple languages **within `/src`**  
- introduce multiple model hosts  
- introduce streaming **at the model-adapter or evaluator level** (token-level streaming from the model API, incremental/partial evaluation, or otherwise executing IR operations out of the deterministic order defined in `evaluator-semantics.md`)  
- introduce batching  
- introduce parallelism  
- introduce caching  
- introduce optimization passes  

These are explicitly out of scope for v0.1. The `/ui` component is the one deliberate, pre-approved exception to the single-language rule — see §1.1.

**Scope note on streaming:** the prohibition above applies to the model adapter and evaluator only. `/src/api`'s WebSocket event stream (per `spec/api.md`) is a transport-layer concern — it delivers the same pipeline stage outputs, computed in the same order, by the same deterministic one-request-at-a-time execution, just incrementally instead of bundled into one response. It does not add randomness, retries, batching, parallelism, or reordering, so it does not violate this rule.

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
The `/src` pipeline architecture is fixed, deterministic, and single‑language (Rust); `/ui` is a separate, deliberately-scoped Avalonia/.NET client governed by `spec/ux/*.md` (see §1.1).  
Claude must generate code that is consistent, spec‑driven, and free of hidden behavior.