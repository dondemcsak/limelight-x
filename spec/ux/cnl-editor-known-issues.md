# CNL Editor — Known Issues

## Purpose

Tracks issues discovered during implementation of the CNL editor (`/ui`) that are real, understood, and deliberately deferred — not forgotten, not silently worked around. Each entry should have enough detail that a future session (human or Claude) can pick it up without re-deriving the investigation.

This is a running log, not a spec — it does not define required behavior the way `spec/ux/*.md` do. If an issue here is ever fixed, move its entry to a "Resolved" section at the bottom (with the fix's date and commit/PR reference) rather than deleting it, so the investigation history stays visible.

---

# Open Issues

## 1. Missing-closing-quote diagnostic never reaches a clean `MISSING` node

**Discovered:** 2026-07-10, while implementing squiggly-underline/hover/ghost-text suggestions for Tree-sitter diagnostics (`bdd-ui-interactions.md` §2.16–§2.19).

**Symptom:** `DiagnosticService`'s self-describing-fix table (`ui-intellisense-engine-spec.md` §6.1) has an entry mapping a `MISSING '"'` node to `"Missing closing quote."` + a `"` suggested fix (`ui/intellisense/DiagnosticService.cs`'s `SelfDescribingMissingLiterals`). In practice, **no input tried produces a `MISSING '"'` node** — an unterminated string always collapses into one blanket `ERROR` node spanning far more than just the missing quote, so this table entry is currently dead code. The period (`.`) and closing-brace (`}}`) entries in the same table both work correctly and are covered by tests (`ui/tests/Intellisense/DiagnosticServiceTests.cs`).

**Root cause:** the `string` rule's content sub-rule, `repeat(/[^"]/)`, was unboundedly greedy — with no closing quote anywhere in the remaining buffer, it consumed everything up to true EOF trying to find one, giving Tree-sitter's GLR error recovery no local, bounded failure point to insert a synthetic `MISSING '"'` at.

**Fix attempted:** bounded the content regex at newlines — `repeat(/[^"\n]/)` — in `tree-sitter/grammar.js` and its spec copy `spec/parsing/grammer-js.md`, matching the boundary `_free_text_word` already uses (`/[^\s.\n]+/`). Rebuilt `tree-sitter-limelightx.dll` (both `tree-sitter/src/` and `ui/native/` copies) and re-verified against the real parser.

**Result: fix did not work.** Confirmed via two separate probes:
- A "sanity" case (`"abc\n"` — a *valid* string with a newline between its content and closing quote) parses with **zero errors**, confirming the grammar change correctly treats the newline as skippable `extras` before the closing-quote token on the happy path.
- A clean single-error case (`Load the article from "a.txt\n.` — quote missing, but the sentence's final period is right there after the newline, so exactly one thing is wrong, the same shape as the working period/`}}` cases) **still produces one blanket `ERROR` node**, not a `MISSING '"'`.

This means the closing `"` — despite being just as atomic an anonymous literal token as `.` or `}}` — is treated differently by Tree-sitter's GLR recovery cost heuristic in this position, for reasons not determined. Further diagnosis would require instrumenting or reading Tree-sitter's own recovery-heuristic source, which wasn't pursued.

**Decision:** keep the `tree-sitter/grammar.js` newline-bounding change (harmless, arguably still a minor correctness improvement — a string literal probably shouldn't span a line break anyway, and no spec example anywhere relies on that). Keep the dead `SelfDescribingMissingLiterals["\""]` table entry in `DiagnosticService.cs` as-is rather than ripping it out — it's correct code for the case where the real parser does someday produce a `MISSING '"'` node, and behaves correctly (falls through to the generic `"Missing expected token."` message) for every input observed so far. No test exists for it (would need to assert unreachable/negative behavior, which isn't useful).

**Possible future approaches (not attempted):**
- A Tree-sitter external scanner (`scanner.c`) that explicitly handles unterminated strings — out of scope per `CLAUDE.md`'s "never generate `scanner.c` unless grammar requires it."
- A `DiagnosticService`-level text heuristic: for a generic `ERROR` node, inspect its raw source-text span for an unmatched leading `"` and synthesize the message/fix from that, instead of relying on Tree-sitter's `MISSING`-node mechanism at all. Deferred because it departs from `DiagnosticService`'s current CST-only design (no raw-text scanning anywhere else in the class) and is inherently more fragile (has to guess an insertion point rather than reading one off a node's span).

**Impact:** a user who forgets a closing quote in a CNL resource path or prompt string still gets a squiggly + "Unexpected token." hover (the generic `ERROR`-node path, `bdd-ui-interactions.md` §2.16–§2.17 both still work) — they just don't get the more specific message or the ghost-text suggested fix that period/`}}` get.

**References:** `tree-sitter/grammar.js`, `spec/parsing/grammer-js.md`, `ui/intellisense/DiagnosticService.cs`, `ui/tests/Intellisense/DiagnosticServiceTests.cs`, `spec/ux/ui-intellisense-engine-spec.md` §6.1.

---

# Resolved Issues

*(none yet)*
