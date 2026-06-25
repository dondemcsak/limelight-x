# Coding Standards

## Purpose
This document defines the coding standards for the Limelight‑X codebase.  
All Rust code generated or modified by Claude must follow these rules.  
These standards ensure consistency, readability, determinism, and alignment with the architectural constraints defined in `/spec`.

These rules are **mandatory** for all code in the repository.

---

# 1. Language & Edition

- All code must target **Rust 2021 edition**.
- Code must compile without warnings under `cargo build` and `cargo clippy`.

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
/spec
    ...
```

### Rules

1. Each module must contain only one conceptual responsibility.
2. Cross‑module imports must follow architectural boundaries:
   - Parser → produces raw AST
   - Normalizer → consumes raw AST, produces normalized AST
   - IR compiler → consumes normalized AST, produces IR
   - Evaluator → consumes IR
   - Model adapter → only called by evaluator
3. No circular dependencies.
4. No “misc”, “utils”, or “helpers” modules unless explicitly approved.
5. No `/ast` module — AST node definitions live inside the parser and normalizer modules as specified in `architecture.md`.

---

# 3. Naming Conventions

### Files & Modules
- `snake_case` for files and modules.
- One module per file unless the spec requires otherwise.

### Types
- `PascalCase` for structs, enums, traits.

### Functions & Variables
- `snake_case` for functions and variables.
- Avoid abbreviations unless widely accepted (`ir`, `cli`, `ast`).

### Constants
- `SCREAMING_SNAKE_CASE`.

---

# 4. Error Handling

All fallible operations must return:

```
Result<T, Error>
```

### Error Type Rules
- Use a single crate‑wide error enum: `crate::error::Error`.
- Implement errors using the `thiserror` crate.
- Error messages must be human‑readable and actionable.
- No `unwrap()`, `expect()`, or panics in production code.

### Fatal Errors
- Must halt evaluation immediately.
- Must include:
  - operation index  
  - operation type  
  - human‑readable message  

---

# 5. Function Design

### General Rules
- Functions must be small and cohesive.
- Prefer pure functions unless side effects are required.
- Avoid deeply nested logic.
- Avoid long parameter lists; prefer structs for configuration.

### Async Rules
- Use `async` only in evaluator and model adapter layers.
- Parser, normalizer, and IR compiler must remain synchronous.

---

# 6. Data Structures

### Structs
- Must derive:
  - `Debug`
  - `Clone`
  - `PartialEq` (when appropriate)
  - `Serialize` / `Deserialize` (when required)

### Enums
- Must be exhaustive.
- Must not include catch‑all variants like `Other`.

### References
- Use owned `String` for IR fields unless the spec requires borrowing.

### AST Nodes
- Must follow the definitions in `architecture.md`.
- Normalized AST must use explicit `InputRef` and `ExpressionRef` types.

---

# 7. Documentation

### Rustdoc
Every public type, function, and module must include Rustdoc comments:

```
/// One‑sentence summary.
/// Additional details as needed.
```

### Examples
- Include examples for public APIs when helpful.
- Examples must compile.

---

# 8. Testing Standards

### Test Types
- Unit tests for:
  - parser
  - AST normalization
  - IR compilation
  - evaluator deterministic behavior

- Integration tests for:
  - CLI
  - end‑to‑end `.llx` execution

### BDD Integration
- Every scenario in `spec/bdd.md` must map to a test.
- Tests must follow the Given/When/Then structure in comments.

### Test Rules
- No mocking of the parser or IR compiler.
- Model calls must be stubbed unless explicitly testing the adapter.
- Normalizer tests must validate pronoun resolution, variable binding, and implicit input rules.

---

# 9. Dependencies

### Allowed Crates
- `serde`
- `serde_json`
- `thiserror`
- `tokio`
- `reqwest` (for model adapter)
- `clap` (for CLI)
- `regex` (for parser, if needed)

### Prohibited Crates
- Any crate that introduces nondeterminism.
- Any crate that hides control flow.
- Any crate that performs implicit I/O.

### Adding Dependencies
Claude must not add new dependencies unless explicitly instructed.

---

# 10. Code Style

### Formatting
- Must follow `rustfmt` defaults.
- No custom formatting rules.

### Imports
- Group imports by:
  1. std
  2. external crates
  3. internal modules

### Comments
- Use comments to explain *why*, not *what*.

---

# 11. Logging & Output

- No logging in parser, normalizer, or IR compiler.
- Evaluator may log only in trace mode.
- CLI must handle all user‑facing output.

---

# 12. Prohibited Patterns

Claude must **never** generate:

- `unwrap()`, `expect()`, or panics  
- global mutable state  
- macros (unless explicitly allowed)  
- unsafe Rust  
- dynamic dispatch unless required  
- trait objects for core pipeline components  
- implicit conversions  
- hidden side effects  

---

# Summary
These coding standards ensure that Limelight‑X remains deterministic, maintainable, and aligned with the architectural and behavioral specifications.  
Claude must follow these rules for all code generation and modification.