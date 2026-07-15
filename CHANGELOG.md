# Changelog

All notable changes to Limelight‑X are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

---

## [0.5.1] — Initial Public Release

Limelight‑X's first public release: a minimal, deterministic expression layer that compiles a small Constrained Natural Language (CNL) into a linear IR and evaluates it via a Claude 3.5 Sonnet model adapter.

### Added
- Core pipeline (`/src`): parser, normalizer, IR compiler, evaluator, and Claude model adapter, implementing the full CNL → Raw AST → Normalized AST → IR → Result pipeline.
- `llx` CLI with `run`, `explain`, `trace`, and `serve` commands.
- `/src/api` — a local HTTP server (`llx serve`) exposing `run`/`explain`/`trace` over `/run`, `/explain`, `/trace`, plus incremental WebSocket streaming of pipeline stage events.
- Optional Avalonia/.NET desktop client (`/ui`): tabbed CNL editor with Tree‑sitter‑backed syntax highlighting, completion, diagnostics, hover, folding, and go‑to‑definition; AST/IR inspector; run/explain/trace execution against `/src/api`.
- Windows MSIX packaging (`win-x64` and `win-arm64`) for the desktop client.
- Full spec suite under `/spec` covering grammar, normalization, IR, evaluator semantics, model adapter, API, coding standards, and UI architecture/UX.
