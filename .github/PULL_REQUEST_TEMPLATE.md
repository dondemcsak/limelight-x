# Pull Request: Limelight‑X

Thanks for contributing. Please fill this out so reviewers have the context they need.

## 1. Summary
What does this PR change, and why?

---

## 2. Component(s) Touched
- [ ] `/src` (core pipeline: parser / normalizer / ir / evaluator / model / api / cli)
- [ ] `/ui` (Avalonia desktop client)
- [ ] `/spec` (documentation / grammar / semantics)

---

## 3. Related Issue
Closes # (if applicable)

---

## 4. Spec Alignment
- [ ] This PR changes behavior, and the relevant spec in `/spec` was updated in the same change
- [ ] This PR does not change behavior (refactor, fix matching existing spec, docs, etc.)

---

## 5. Testing
- [ ] Tests were added or updated for this change
- [ ] `cargo test --release --locked` passes (if `/src` touched)
- [ ] `dotnet test ui/LimelightX.slnx` passes (if `/ui` touched)

---

## 6. Checklist
- [ ] `cargo fmt --check` / `cargo clippy -- -D warnings` clean (if `/src` touched)
- [ ] `dotnet format --verify-no-changes` clean (if `/ui` touched)
- [ ] No new dependencies added without prior discussion (see `CLAUDE.md` §3.5)

---

## 7. Additional Notes (optional)
Anything else reviewers should know.
