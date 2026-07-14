# Contributing to Limelight‑X

Thanks for your interest in Limelight‑X. This document explains how the project is structured, how to propose changes, and what's expected of a pull request.

---

## Spec‑Driven Development

Limelight‑X is spec‑driven: all behavior is defined in `/spec`, and **if code and spec disagree, the spec wins**.

This means a behavioral change — a new grammar rule, a new evaluator step, a new CLI flag — starts with a spec change, not a code change. If you're proposing new behavior and the spec doesn't already describe it, open a **DSL Proposal** issue first (see below) before writing code.

Read `spec/architecture.md` for the pipeline overview and `README.md` §8 ("Specs Are Authoritative") for the full list of key specs before making a change.

---

## Two Components, Two Rule Sets

- **`/src`** — the core pipeline (Rust): parser, normalizer, IR compiler, evaluator, model adapter, `/src/api`. Governed by `CLAUDE.md` and `spec/coding-standards.md`. Single‑language, deterministic, no new dependencies without explicit approval.
- **`/ui`** — the optional desktop client (Avalonia/.NET, C#). Governed by `spec/ux/*.md`. Talks to `/src/api` over HTTP; does not reimplement pipeline stages.

Know which component your change touches, and follow that component's rules.

---

## Proposing a Change

For anything beyond a trivial fix, open an issue before opening a PR:

- **Bug Report** — something isn't working as specified
- **Feature Request** — suggest an enhancement or new capability
- **DSL Proposal** — propose a change to the grammar, IR, evaluator, or semantics

See `.github/ISSUE_TEMPLATE/` for all three. This gives maintainers and other contributors a chance to weigh in on direction before code is written.

---

## Testing Expectations

- Every behavior defined in `spec/bdd.md`, `spec/bdd-api.md`, and `spec/ux/bdd-ui-*.md` should map to a test — one test per scenario.
- Tests must be deterministic and must never call the real model adapter; use the mock adapter (`/src`) or fakes (`/ui`) already established in the test suites.
- Run the relevant suite locally before opening a PR: `cargo test --release --locked` for `/src`, `dotnet test ui/LimelightX.slnx` for `/ui`.

---

## Pull Requests

1. Fork and branch from `main`.
2. Make your change, following the component's rules above.
3. Fill out the PR template — it asks which component(s) you touched, whether the spec changed, and whether tests were added.
4. Expect CI (`.github/workflows/ui-ci.yml`) to run lint, format, dependency-audit, and test checks against both components before merge.

---

## Code of Conduct

Participation in this project is governed by `CODE_OF_CONDUCT.md`. Please read it before contributing.

## Questions

Not sure where to start, or have a question that isn't a bug or feature request? See `SUPPORT.md`.
