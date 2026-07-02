# UI Build Pipeline

## Purpose
This document defines the deterministic build pipeline for the Limelight‑X UI.  
The UI (`/ui`) is a separate Avalonia/.NET (C#) project built with the **.NET SDK / MSBuild**, and is packaged together with the Rust CLI server binary (`/src/api`, built with **Cargo**, per `spec/api.md`) into a single MSIX installer via **GitHub Actions**.  
The pipeline supports Windows‑only builds, produces MSIX installers, uses semantic versioning, and publishes stable‑channel artifacts.

The pipeline is organized by **workflow**:
- Local Build Workflow  
- CI Build Workflow  
- Release Build Workflow  

---

# 1. Build Overview

Limelight‑X uses two build systems, one per component:
- **.NET SDK / MSBuild** (`dotnet build`) as the build system for `/ui` (Avalonia)  
- **Cargo** as the build system for `/src`, including the `/src/api` server consumed by `/ui`  
- **GitHub Actions** for CI + artifact publishing  
- **Semantic versioning** for releases  
- **MSIX packaging** for distribution, bundling both build outputs  
- **`packages.lock.json` (NuGet) and `Cargo.lock`** for dependency locking  
- **Unit tests** for validation, run separately per component  
- **Full static analysis** (`dotnet format`, Roslyn analyzers, Rustfmt, Clippy, `cargo audit`, NuGet vulnerability audit)

Build artifacts:
- UI executable: `LimelightX.exe` (project `LimelightX.UI`, `/ui`, .NET — see `ui-architecture.md` §3)  
- CLI/server binary: `llx.exe` (`/src`, Rust — the same binary as the CLI; `llx serve` is what runs it as `/src/api`'s HTTP server)  
- MSIX installer bundling both

---

# 2. Local Build Workflow

## 2.1. Prepare Stage
- Ensure the .NET SDK is installed  
- Ensure the Rust toolchain is installed  
- Ensure `packages.lock.json` and `Cargo.lock` are present  
- Ensure no dependency drift exists in either component

## 2.2. Compile Stage
- Run `dotnet build -c Release` for `/ui`  
- Run `cargo build --release` for `/src` (including `/src/api`)  
- Validate XAML structure  
- Run `dotnet format --verify-no-changes` and Roslyn analyzers for `/ui`  
- Run Rustfmt and Clippy for `/src`

## 2.3. Test Stage
- Run `dotnet test` for `/ui`  
- Run `cargo test` for `/src` (including `/src/api`)  
- Fail build on any test failure in either component  
- Fail build on any static analysis error

## 2.4. Package Stage
- Generate MSIX installer  
- Bundle UI executable + CLI server binary  
- Validate installer structure

---

# 3. CI Build Workflow (GitHub Actions)

## 3.1. Trigger Conditions
- Push to `main`  
- Pull request targeting `main`  
- Manual dispatch for release builds

## 3.2. Prepare Stage
- Checkout repository  
- Restore NuGet cache (`/ui`)  
- Restore Cargo cache (`/src`)  
- Validate `packages.lock.json` and `Cargo.lock`  
- Run dependency audits for both components

## 3.3. Compile Stage
- Build UI executable via `dotnet build -c Release` (Windows target)  
- Build CLI server binary via `cargo build --release`  
- Validate XAML  
- Run `dotnet format` / Roslyn analyzers, and Rustfmt / Clippy  
- Fail CI on any formatting or linting error in either component

## 3.4. Test Stage
- Run `dotnet test` for `/ui`  
- Run `cargo test` for `/src`  
- Fail CI on any test failure

## 3.5. Package Stage
- Generate MSIX installer  
- Bundle UI executable + CLI server binary  
- Validate installer manifest  
- Validate installer signing status (unsigned)

## 3.6. Artifact Publishing Stage
- Upload UI executable  
- Upload CLI server binary  
- Upload MSIX installer  
- Mark artifacts as stable‑channel builds

---

# 4. Release Build Workflow

## 4.1. Versioning Stage
- Increment semantic version (major.minor.patch)  
- Tag release commit with version  
- Push tag to repository

## 4.2. Build Stage
- Run full CI build workflow  
- Ensure no warnings or errors remain in either component  
- Ensure MSIX installer is generated

## 4.3. Publish Stage
- Publish artifacts to GitHub Releases  
- Include UI executable, CLI server binary, MSIX installer  
- Include release notes  
- Mark release as stable channel

---

# 5. Static Analysis Requirements

## 5.1. `/ui` (.NET)
- All C# code must pass `dotnet format --verify-no-changes`  
- All Roslyn analyzer warnings must be resolved  
- CI fails on formatting drift or analyzer errors

## 5.2. `/src` (Rust)
- All Rust code must be formatted with Rustfmt  
- All Clippy warnings must be resolved  
- CI fails on formatting drift or any Clippy error

## 5.3. XAML Validation
- All Avalonia XAML must pass structural validation  
- CI fails on any XAML error

## 5.4. Dependency Audit
- `dotnet list package --vulnerable` must pass for `/ui`  
- `cargo audit` must pass for `/src`  
- CI fails on any critical vulnerability in either component

---

# 6. Dependency Management

## 6.1. Lockfile Enforcement
- `packages.lock.json` (NuGet, `/ui`) and `Cargo.lock` (`/src`) must both be present  
- CI fails if either lockfile is modified without commit  
- No vendoring required  
- No hash verification required beyond standard NuGet/Cargo checks

## 6.2. Dependency Updates
- Updates must be intentional  
- Updates must be validated through CI  
- Updates must not break MSIX packaging

---

# 7. Build Targets

## 7.1. Windows‑Only UI Build
- `LimelightX.UI` targets Windows (`net8.0-windows` or later) and produces `LimelightX.exe`  
- Avalonia build uses Windows runtime  
- No macOS or Linux UI builds

## 7.2. CLI/Server Build
- `llx.exe` built for Windows via Cargo (the same binary the CLI uses; `llx serve` runs its `/src/api` server mode)  
- Optional cross‑platform builds may be added later

---

# 8. Packaging Requirements

## 8.1. MSIX Installer
- Installer must include `LimelightX.exe`  
- Installer must include `llx.exe`  
- Installer must include manifest  
- Installer must remain unsigned  
- Installer must validate structure before publishing

## 8.2. Distribution Bundle
- `LimelightX.exe` + `llx.exe` must be bundled  
- Bundle must match MSIX contents  
- Bundle must be published as CI artifact

---

# 9. Release Channel

## 9.1. Stable Channel Only
- Only stable releases are produced  
- No beta, nightly, or experimental channels  
- All releases must be tagged  
- All releases must include artifacts

---

# 10. Build Pipeline Determinism

## 10.1. Standard Builds Only
- Deterministic builds not required  
- Reproducibility not enforced  
- Dependency locking ensures basic stability

---

# Summary

The Limelight‑X UI build pipeline uses two build systems — .NET SDK/MSBuild for `/ui` and Cargo for `/src` (including `/src/api`) — orchestrated through GitHub Actions to produce Windows‑only builds, MSIX installers, and a bundled CLI server binary.  
It enforces semantic versioning, unit testing per component, full static analysis, dependency locking, and stable‑channel releases.  
The pipeline is organized by workflow (local, CI, release) and must be followed exactly.