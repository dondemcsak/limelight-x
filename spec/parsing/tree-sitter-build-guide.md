# Limelight‑X Tree‑sitter DLL Build Guide

This document provides step‑by‑step instructions for generating the **Tree‑sitter parser DLL** for the Limelight‑X CNL editor.

This is a build guide only — it does not define the runtime architecture. See `spec/cnl-editor-architecture.md` (parent authority) and `spec/parsing/tree-sitter-integration.md` (P/Invoke integration) for how the resulting DLL is consumed.

Assumptions:

- You have a folder named `tree-sitter/` at the repo root containing the four Tree‑sitter grammar files (`grammar.js`, `highlights.scm`, `injections.scm`, `folds.scm`) plus the `package.json`/`tree-sitter.json` config `tree-sitter init`/`tree-sitter generate` produce.
- You want to produce `tree-sitter-limelightx.dll` and copy it into `ui/native/`.

---

## 0. Build Target: ARM64 Now, x64 Later (Staged)

**Current status: ARM64 only.** The DLL committed today is built for ARM64 (matching the primary dev machine, a Copilot+ PC). This section documents that build.

**A `win-x64` build is separate, deferred future work — not yet done.** `spec/ux/ui-build-pipeline.md` §7.1 pins `LimelightX.UI.csproj` to `RuntimeIdentifier=win-x64` (matching the `windows-latest` CI runner) for a deliberate, documented reason (MSIX size). An ARM64 `tree-sitter-limelightx.dll` P/Invoked from that x64 process throws `BadImageFormatException` at load time. Until the x64 build exists (§9 below is a placeholder for it):

- Any code path or test that loads `tree-sitter-limelightx.dll` must be skippable/gated on CI (which runs `windows-latest`, i.e. x64).
- Local development and manual testing of the Tree‑sitter integration happens on ARM64 hardware only.

This staging is intentional (see `CLAUDE.md` §3.5's "Also explicitly approved for `/ui`" entry) — it is not an oversight to "fix" by rebuilding for x64 as part of a routine grammar change.

---

## 1. Folder Structure

Ensure the following structure exists at the repo root:

```
tree-sitter/
  grammar.js
  highlights.scm
  injections.scm
  folds.scm
  package.json
  tree-sitter.json
```

---

## 2. `package.json`

`tree-sitter init` (or `tree-sitter generate` against an existing `package.json`) produces a fuller manifest than the minimal example below — the actual committed `tree-sitter/package.json` additionally has `type`, `repository`, `files`, `dependencies` (`node-addon-api`, `node-gyp-build`), `devDependencies` (`prebuildify`, `tree-sitter`, `tree-sitter-cli`), and `scripts`. The fields that matter for this guide:

```json
{
  "name": "tree-sitter-limelightx",
  "version": "0.1.0",
  "description": "Tree-sitter grammar for Limelight-X CNL",
  "main": "grammar.js",
  "keywords": ["tree-sitter", "limelightx", "cnl"],
  "license": "MIT"
}
```

---

## 3. Install Tree‑sitter CLI

```bash
npm install -g tree-sitter-cli
tree-sitter --version
```

---

## 4. Generate the C Parser Code

From inside the `tree-sitter/` folder:

```bash
cd tree-sitter
tree-sitter generate
```

This produces `tree-sitter/src/parser.c`, `src/grammar.json`, and `src/node-types.json`.

> Note: the Limelight‑X grammar does **not** require `scanner.c`. Only `parser.c` is generated — this is expected.
>
> **Any change to `tree-sitter/grammar.js` must be re-generated and rebuilt before the DLL reflects it.** There is no automated step that does this — see `spec/cnl-editor-architecture.md` §6's two-tier file model for the full hand-sync obligation across `spec/parsing/grammer-js.md`, `tree-sitter/grammar.js`, and the compiled DLL.

---

## 5. Open the Matching Native Tools Command Prompt

For the current ARM64 build:

```
ARM64 Native Tools Command Prompt for VS 2022
```

Navigate to the generated source folder:

```bash
cd path\to\tree-sitter\src
```

---

## 6. Build the DLL Using MSVC

```bash
cl /LD /I .. parser.c /Fe:tree-sitter-limelightx.dll
```

Explanation:

- `cl` — MSVC compiler
- `/LD` — produce a DLL
- `/I ..` — include parent folder (Tree‑sitter headers, `tree_sitter/*.h`)
- `parser.c` — generated parser
- `/Fe:` — output DLL name

If successful, `tree-sitter-limelightx.dll` (plus `.exp`/`.lib`/`.obj` build byproducts) appear in the `src` folder. These byproducts are git-ignored by `tree-sitter/.gitignore`; only the DLL is copied onward in the next step.

---

## 7. Copy the DLL to the Avalonia Project

```
ui/native/tree-sitter-limelightx.dll
```

This is the path `LimelightX.UI.csproj` references (see `spec/parsing/tree-sitter-integration.md` §8).

---

## 8. Load the DLL in Avalonia (Raw P/Invoke — No TreeSitterSharp)

Per `CLAUDE.md` §3.5, no third-party Tree‑sitter binding NuGet package (e.g. TreeSitterSharp) is approved. Bindings are hand-written `[DllImport]` declarations against the DLL directly, matching `spec/parsing/tree-sitter-integration.md` §4:

```csharp
[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr tree_sitter_limelightx();

[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr ts_parser_new();

[DllImport("tree-sitter-limelightx.dll")]
public static extern bool ts_parser_set_language(IntPtr parser, IntPtr language);

[DllImport("tree-sitter-limelightx.dll")]
public static extern IntPtr ts_parser_parse_string(IntPtr parser, IntPtr oldTree, string input, UIntPtr length);
```

```csharp
public sealed class LimelightXParser : IDisposable
{
    private readonly IntPtr _parser;
    private readonly IntPtr _language;

    public LimelightXParser()
    {
        _parser = ts_parser_new();
        _language = tree_sitter_limelightx();
        ts_parser_set_language(_parser, _language);
    }

    public IntPtr Parse(string text) =>
        ts_parser_parse_string(_parser, IntPtr.Zero, text, (UIntPtr)text.Length);

    public void Dispose() => ts_parser_delete(_parser);

    [DllImport("tree-sitter-limelightx.dll")]
    private static extern void ts_parser_delete(IntPtr parser);
}
```

This enables:

- incremental parsing
- syntax highlighting
- folding
- IntelliSense context
- error nodes

The full binding surface (query engine, node accessors, memory management) is documented in `spec/parsing/tree-sitter-integration.md` §§4–6, 10–11 — this guide only covers building and loading the DLL itself.

---

## 9. Future: `win-x64` Build (Not Yet Done)

Placeholder for when the x64 build is added:

- Repeat §§4–6 from an **`x64 Native Tools Command Prompt for VS 2022`** instead of the ARM64 one.
- The resulting DLL is a *different* binary than the ARM64 one — both may need to coexist (e.g. `ui/native/win-x64/tree-sitter-limelightx.dll` vs. `ui/native/win-arm64/tree-sitter-limelightx.dll`), with the `.csproj` selecting the right one per `$(RuntimeIdentifier)`. Exact layout is undecided — do not implement this section speculatively; it needs its own explicit instruction when the work is actually scheduled.
- Once it exists, remove the CI-gating language in §0 for the `windows-latest` (x64) runner.

---

## 10. Summary

Steps to build the Tree‑sitter DLL (current: ARM64):

1. Place the four grammar files in `tree-sitter/`.
2. Ensure `package.json`/`tree-sitter.json` exist (`tree-sitter init` generates these).
3. Run `tree-sitter generate`.
4. Open **ARM64 Native Tools Command Prompt for VS 2022**.
5. Build the DLL: `cl /LD /I .. parser.c /Fe:tree-sitter-limelightx.dll`
6. Copy the DLL into `ui/native/tree-sitter-limelightx.dll`.
7. Load it via raw P/Invoke (§8) — never TreeSitterSharp or any other third-party binding.

This DLL powers the editor‑side IntelliSense and syntax features for Limelight‑X CNL, client‑side only, per `spec/cnl-editor-architecture.md` §5.
