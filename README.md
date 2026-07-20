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

### Key points

- There is **no `/ast` module** — AST types live in `/parser` and `/normalizer`.
- There is **no provider layer** — the evaluator calls the model adapter directly.
- `/src/api` is a thin HTTP wrapper around the existing `run`/`explain`/`trace` logic (see `api.md`) — it is the one approved exception to the "no new modules" rule.
- `/ui` is a separate, optional Avalonia/.NET (C#) desktop client (see `spec/ux/`) that talks to `/src/api` over local HTTP. It is a deliberately-scoped second language; the core pipeline in `/src` remains single-language Rust.
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

## `llx serve [--port <N>]`
Starts a local, loopback-only HTTP server (`/src/api`, see `api.md`) exposing `POST /run`, `/explain`, `/trace` — the same three operations above, over HTTP. It exists so the optional `/ui` desktop client (see §11) can drive the pipeline without embedding Rust. Default port is `4747`.

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
- `api.md` — `/src/api` HTTP server (`llx serve`), consumed by the optional `/ui` client  
- `bdd.md` — acceptance criteria for the core pipeline  
- `bdd-api.md` — acceptance criteria for `/src/api`  
- `ux/` — specs for the optional `/ui` desktop client (Avalonia/.NET)  

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

# 10. Optional UI

Limelight‑X also has an optional desktop UI (`/ui`), a separate Avalonia/.NET (C#) client that talks to `llx serve`'s local HTTP API instead of embedding Rust. It is entirely optional — the CLI is fully self-sufficient without it. See `spec/ux/` for its specs and `api.md` for the HTTP contract it depends on.

---

# 11. Building & CI

Full details are the authoritative spec at `spec/ux/ui-build-pipeline.md`. Summary below.

## 11.1 CI Build

Every push and pull request to `main` runs the CI workflow (`.github/workflows/ui-ci.yml`), which builds and validates **both** components together as a **`win-x64` / `win-arm64` matrix** — one leg on `windows-latest`, one on `windows-11-arm` (GitHub-hosted, free for public repos). Each leg runs the full sequence independently, since `cargo build --release` always targets its own runner's host architecture:

- **Prepare** — checkout, restore .NET/Cargo caches, validate `Cargo.lock` / `packages.lock.json` are present and committed, restore both components in locked mode, run dependency audits (`cargo audit`, `dotnet list package --vulnerable`)
- **Compile** — `dotnet build ui/LimelightX.slnx -c Release -warnaserror`, `dotnet format --verify-no-changes`, `cargo build --release --locked`, `cargo fmt --check`, `cargo clippy -- -D warnings`
- **Test** — `dotnet test`, `cargo test --release --locked` (each leg's `NativeTreeSitter`-tagged `/ui` tests exercise that architecture's own Tree-sitter DLLs for real)
- **Package** — `dotnet publish -r <rid>` for `LimelightX.UI`, then `ui/packaging/stage-bundle.ps1 -Rid <rid>` stages `LimelightX.exe` + `llx.exe` + dependencies into a portable bundle directory, followed by structural validation of the staged bundle
- **Publish artifacts** — uploads the staged `LimelightX.exe` + `llx.exe` bundle as a CI artifact, RID-suffixed per leg (`limelight-x-bundle-stable-win-x64`/`-win-arm64`)

Tagged pushes matching `v*.*.*` additionally trigger `.github/workflows/ui-release.yml`, which reuses this CI workflow and publishes both architectures' artifacts (`LimelightX-win-x64.zip`/`LimelightX-win-arm64.zip`) to a GitHub Release (stable channel only). There is no installer — the ZIP is extracted to any folder and run directly; see `spec/ux/ui-deployment.md`.

## 11.2 Manual Testing (Debug) Build

Running the full CI-equivalent build locally just to manually try out a change is unnecessary overhead — it lints, audits, tests, and stages a packaged bundle. For quick local iteration, use:

```
./scripts/build-manual-testing.ps1
```

This builds `/src` (`cargo build`) and `/ui` (`dotnet publish ui/LimelightX.UI.csproj --no-self-contained`) in **Debug** configuration by default (pass `-Configuration Release` for a release build), and stages `LimelightX.UI.exe`, `llx.exe`, and every required runtime dependency (DLLs, `.deps.json`, `.runtimeconfig.json`) together into `target/manual-testing/` so the app can be run from one folder. It does not lint, audit, test, or package — see `spec/ux/ui-build-pipeline.md` §2.5 for the full spec.

---

# 12. License

MIT License.

---

# Summary

Limelight‑X is a transparent, deterministic expression layer that demonstrates how CNL can be compiled into IR and executed through a model adapter.  
It is fully spec‑driven, easy to understand, and ideal for learning how expression layers work.