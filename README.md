# Limelight‑X

Limelight‑X is a **minimal, deterministic expression layer** that compiles a small Constrained Natural Language (CNL) into a linear Intermediate Representation (IR) and evaluates it using a combination of local logic and a Claude 3.5 Sonnet model adapter.

It is intentionally small, transparent, and spec‑driven — a reference implementation of how an expression layer works.

---

# 1. What Limelight‑X Does

Limelight‑X takes natural‑language‑ish instructions like:

```
Load the article from "article.txt".
Extract the entities.
Summarize them using {{ prompt: "Summarize in 3 bullets." }}.
```

And compiles them through a deterministic pipeline:

```
CNL → Parser → Raw AST → Normalizer → Normalized AST → IR Compiler → IR → Evaluator → Model Adapter → Result
```

Every stage is isolated, explicit, and fully specified in `/spec`.

---

# 2. Project Goals

Limelight‑X is designed to:

- demonstrate how a CNL can be parsed into a structured AST  
- show how ambiguity is removed through normalization  
- show how a canonical IR is produced  
- show how deterministic evaluation works  
- show how prompts are constructed  
- show how a model adapter integrates with Claude  
- provide full transparency via `llx explain` and `llx trace`  

It is **not** a production expression layer.  
It is a **teaching and demonstration engine**.

---

# 3. Repository Structure

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

### Key points

- There is **no `/ast` module** — AST types live in `/parser` and `/normalizer`.
- There is **no provider layer** — the evaluator calls the model adapter directly.
- All behavior is defined in `/spec`.

---

# 4. The Pipeline

## 4.1 Parser → Raw AST
- Implements grammar from `cnl-grammar.md`
- Produces raw AST with:
  - pronouns  
  - NamedVariable  
  - Bind  
  - implicit inputs  

## 4.2 Normalizer → Normalized AST
- Implements `ast-normalizer.md`
- Resolves:
  - pronouns → `PreviousResult`
  - NamedVariable → underlying `InputRef`
  - implicit inputs → `PreviousResult`
- Removes Bind nodes
- Produces a fully explicit AST

## 4.3 IR Compiler → IR
- Implements `ir.md`
- Produces linear IR with `$N` references
- Embeds custom prompts verbatim

## 4.4 Evaluator → Execution
- Implements `evaluator-semantics.md`
- Executes IR deterministically
- Constructs prompts using strict templates
- Calls the model adapter
- Stores results in a vector

## 4.5 Model Adapter → Claude API
- Implements `model-adapter.md`
- Calls Claude 3.5 Sonnet via Messages API
- Uses deterministic parameters:
  - temperature = 0.0  
  - max_tokens = 2048  
  - no system prompt  

---

# 5. CLI Commands

## `llx run <file>`
Runs the full pipeline:

- parse  
- normalize  
- compile  
- evaluate  
- print final result  

## `llx explain <file>`
Shows compilation without evaluation:

- raw AST  
- normalized AST  
- IR  

## `llx trace <file>`
Runs the full pipeline and prints:

- raw AST  
- normalized AST  
- IR  
- constructed prompts  
- model outputs  
- final result  

Trace mode is the best way to understand the system.

---

# 6. Example

Given:

```
Load the article from "article.txt".
Extract the entities.
Summarize them.
```

### Raw AST
```
Load { ... }
Extract { target: "entities", input: None }
Summarize { input: Pronoun("them"), prompt: None }
```

### Normalized AST
```
Load { ... }
Extract { target: "entities", input: PreviousResult }
Summarize { input: PreviousResult, prompt: None }
```

### IR
```
Load { path: "article.txt" }
Extract { target: "entities", input: "$0" }
Summarize { input: "$1", prompt: None }
```

### Evaluator Prompt (Summarize)
```
Summarize the following text clearly and concisely:

<contents of article.txt>
```

---

# 7. Determinism

Limelight‑X is deterministic except for model output.

Deterministic components:

- parser  
- normalizer  
- IR compiler  
- evaluator prompt construction  
- evaluator operation order  
- model adapter configuration  

Nondeterministic component:

- model output only  

---

# 8. Specs Are Authoritative

All behavior is defined in `/spec`.  
If code and spec disagree, **the spec wins**.

Key specs:

- `architecture.md` — pipeline and module boundaries  
- `cnl-grammar.md` — grammar rules  
- `ast-normalizer.md` — reference resolution  
- `ir.md` — IR structure  
- `evaluator-semantics.md` — prompt construction + execution  
- `model-adapter.md` — Claude integration  
- `bdd.md` — acceptance criteria  

---

# 9. Non‑Goals

Limelight‑X v0.1 does **not** support:

- multiple constrained languages  
- multiple model hosts  
- provider abstraction  
- streaming  
- batching  
- parallel execution  
- caching  
- optimization passes  

These may be added in future versions.

---

# 10. License

MIT License.

---

# Summary

Limelight‑X is a transparent, deterministic expression layer that demonstrates how CNL can be compiled into IR and executed through a model adapter.  
It is fully spec‑driven, easy to understand, and ideal for learning how expression layers work.