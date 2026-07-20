# UI Deployment

## Purpose
This document defines how the Limelight‑X UI is deployed to end‑user environments.  
It specifies deployment targets, distribution method, installation requirements, configuration profiles, update strategy, uninstall behavior, and CLI server bundling.

The spec is organized by **deployment stages**:
- Prepare  
- Install  
- Configure  
- Validate  

---

# 1. Deployment Targets

- **Target:** Windows only  
- **UI:** Avalonia‑based desktop application, `LimelightX.UI`, installed as `LimelightX.exe` (see `ui-architecture.md` §3)  
- **API server:** `/src/api`, started via `llx.exe serve`. Bundled with UI (no separate deployment) — see `api.md`.

---

# 2. Distribution Method

- **Primary distribution:** a portable, per‑architecture ZIP bundle — no installer  
- **Channel:** Stable releases only  
- **Source:** Published artifacts from the build pipeline (GitHub Releases or equivalent): `LimelightX-win-x64.zip`, `LimelightX-win-arm64.zip`  
- **Rationale:** the bundle is unsigned (no code‑signing certificate); an unsigned MSIX installer triggers the same SmartScreen friction as an unsigned executable while adding installer complexity for no trust benefit, so the app ships as a plain extract‑and‑run folder instead.

---

# 3. Installation Requirements

- **Prerequisite:** .NET runtime must be installed (the app is a framework‑dependent, non‑self‑contained publish — see `ui-build-pipeline.md` §7.1)  
- **Installer check:** none — there is no installer to check for the runtime. If the .NET runtime is missing, `LimelightX.exe` fails to launch with the OS's standard "you must install .NET to run this application" prompt.  
- **Environment:** Windows 10 or later  
- **Unsigned‑download warning:** since the ZIP (and the `.exe` files inside it) are unsigned, Windows marks the downloaded ZIP with the Mark‑of‑the‑Web. Windows SmartScreen may show a warning on first launch ("Windows protected your PC") — this is expected; the user proceeds via "More info" → "Run anyway", or unblocks the file first (Properties → Unblock, or `Unblock-File` in PowerShell).

---

# 4. Deployment Stages

## 4.1. Prepare

- **Step 1:** Obtain the latest stable release ZIP matching the machine's architecture (`LimelightX-win-x64.zip` or `LimelightX-win-arm64.zip`) from the trusted release channel  
- **Step 2:** Verify download origin (trusted release channel)  
- **Step 3:** Ensure .NET runtime is installed  
- **Step 4:** Confirm user has Windows 10+ with permission to extract files to their chosen location

## 4.2. Install

- **Step 1:** Extract the ZIP to any writable folder the user chooses (Desktop, a Program Files subfolder, a USB drive — no fixed install path is required or assumed)  
- **Step 2:** If Windows SmartScreen warns on first launch (see §3), proceed via "More info" → "Run anyway", or unblock the extracted files first  
- **Step 3:** Confirm `LimelightX.exe` and bundled `llx.exe` are present in the extracted folder — these two files, plus their runtime dependencies (managed DLLs, native Tree‑sitter DLLs, `.scm` query files), are the entire application; nothing is written outside this folder until first launch (§4.3)

## 4.3. Configure

- **Profiles:** Dev, Stage, Prod  
- **Configuration file:** `config.json`, written **next to `LimelightX.exe`** in the same folder the ZIP was extracted to (not `%APPDATA%`), containing:
  ```json
  {
    "port": 4747,
    "logPath": "string",
    "environmentProfile": "Dev | Stage | Prod"
  }
  ```
  This is the only place `Port`/`LogPath`/`EnvironmentProfile` are persisted. `ANTHROPIC_API_KEY` is never written here — it lives only in Windows Credential Manager, under a single shared credential (not one per profile; see `ui-viewmodels.md` §9).  
- **Configuration items:**  
  - Backend port — the bind host is fixed at `127.0.0.1` and is never configurable (see `SECURITY.md`); only the port `llx serve` binds to is editable (`4747` by default, see `api.md` §8)  
  - Log path — see "Persistent log file" below  
  - `ANTHROPIC_API_KEY` — **required**. There is no separate installer-hosted prompt; see §4.4 Step 4 for how first-launch-without-a-key is actually handled. The UI sets the key in the environment of the `llx serve` process it launches. The UI must not start `llx serve` without it, since `api.md` §8/§10 specifies the server refuses to start and exits immediately without this variable.  

- **Persistent log file:** `LogPath` names a **directory**, not a file — the log file within it is always named `Limelight-x-log.txt`. An empty/unset `LogPath` (the default) resolves to `config.json`'s own directory, i.e. the same folder as `LimelightX.exe`; a custom `LogPath` writes to `<LogPath>\Limelight-x-log.txt` instead. This resolution happens at the moment logging is configured — an empty `LogPath` is never rewritten to a concrete value in `config.json`.  
  - **Mechanism:** the UI logs through `Microsoft.Extensions.Logging`'s `ILogger`/`ILoggerFactory`/`LogLevel` abstractions; Serilog (`Serilog.Extensions.Logging` + `Serilog.Sinks.File`) is the configured provider/file sink (see `CLAUDE.md` §3.5's approved-dependency list).  
  - **Retention:** the file is opened in append mode and never truncated — entries accumulate across app restarts indefinitely (no rotation in v0.1).  
  - **Format:** plain text, one line per entry: `[<UTC ISO-8601 timestamp>] [<LogLevel>] <Code>: <Message>`, with `(line L, column C)` appended when the error has a location, and the error's category included in the message. Example: `[2026-07-04T18:22:31Z] [Error] ERR_CNL_PARSE: Missing period. (Category=Pipeline)`.  
  - **Severity mapping:** `UiError.Severity` → `LogLevel`: `Info`→`Information`, `Warning`→`Warning`, `Error`→`Error`, `Fatal`→`Critical`.  
  - **Failure safety:** a failure to create the log directory or write to the log file must never surface as a user-facing error, crash the app, or block any other functionality — it fails silently. See `ui-error-handling.md` for what gets logged.
- **Selection:** Environment profile chosen via `config.json` at any time, or edited live via the **in-app Settings modal** (`ui-viewmodels.md` §9, `ui-routing-navigation.md` §8) — the Settings modal edits the same `config.json` file plus Credential Manager, and applies changes by restarting `llx serve` in the background.

## 4.4. Validate

- **Step 1:** Launch Limelight‑X UI (double‑click `LimelightX.exe`, or a user‑created shortcut to it — the app creates no shortcuts of its own, since there is no installer to do so)  
- **Step 2:** Confirm main window renders without errors  
- **Step 3:** Confirm environment profile is active (e.g., backend port)  
- **Step 4:** Confirm `llx serve` started successfully. If `config.json` is missing/invalid or `ANTHROPIC_API_KEY` is unset (first launch, or a broken config), `LimelightX.exe` auto‑opens the Settings modal on launch instead of restoring the last workspace. The Explorer and Tab Strip remain fully usable in this state (browsing folders and opening tabs needs no backend), but Run/Explain stay disabled on every `.llx` tab until the user saves valid Settings (see `ui-routing-navigation.md` §10). This is the only first-run experience; there is no separate onboarding page or installer-hosted wizard.  
- **Step 5:** Optionally run a simple pipeline to confirm connectivity

---

# 5. Update Strategy

- **Model:** Manual — there is no installer to drive auto‑update  
- **Behavior:**  
  - The user checks the release channel for a newer stable version and downloads the matching ZIP  
  - Extracting the new ZIP **on top of** the existing folder (overwriting `LimelightX.exe`, `llx.exe`, and their dependencies, but leaving files the ZIP doesn't contain untouched) preserves `config.json` and therefore the configured environment profile — this is the recommended update path  
  - Extracting to a **fresh** folder instead (or deleting the old folder first) starts from a clean `config.json`; the `ANTHROPIC_API_KEY` in Windows Credential Manager is unaffected either way (§6), so re‑entering it is not required even after a clean extract

---

# 6. Uninstall Behavior

- **Mode:** delete the extracted folder — there is no installer, so there is no uninstaller to run  
- **Deleting the folder removes:**  
  - Application binaries (`LimelightX.exe`, `llx.exe`, all dependencies)  
  - `config.json`  
  - The log file at whatever `LogPath` resolves to per §4.3 (the same folder, by default)  
  - Any other files the app wrote alongside itself  
- **What intentionally survives folder deletion:** the `ANTHROPIC_API_KEY` entry in Windows Credential Manager (target name `LimelightX/AnthropicApiKey`, `CRED_PERSIST_LOCAL_MACHINE`). This is deliberate — the credential is scoped to the machine, not to any install folder, so re‑extracting the app later (including after an update per §5) does not require re‑entering the key. A user who wants the key fully removed must delete it explicitly via Windows Credential Manager (Control Panel → Credential Manager → Windows Credentials → remove `LimelightX/AnthropicApiKey`) or `cmdkey /delete:LimelightX/AnthropicApiKey`.  
- **Expectation:** deleting the folder returns the machine to its pre‑extract state, with the single documented exception above.

---

# 7. API Server Deployment

- **Bundling:** `llx.exe` is bundled alongside `LimelightX.exe` in the same ZIP/folder  
- **Location:** Installed alongside the UI binary  
- **Usage:** `LimelightX.exe` launches `llx.exe serve` locally (default `127.0.0.1:4747`, see `api.md` §8) when the UI starts, and terminates it when the UI exits; no separate deployment or service mode  
- **Secrets:** `ANTHROPIC_API_KEY` is supplied to the `llx.exe serve` process via the environment, sourced from the value configured in §4.3

---

# Summary

This UI deployment specification defines Windows‑only, ZIP‑based portable installation of `LimelightX.exe` with .NET prerequisites, environment profiles (Dev/Stage/Prod), manual updates, folder‑delete uninstall, and a bundled `llx.exe` server launched locally via `llx.exe serve`.  
`config.json` and the log file are colocated with `LimelightX.exe` so the extracted folder is fully self‑contained and fully removable; the `ANTHROPIC_API_KEY` in Windows Credential Manager is the single deliberate exception that survives folder deletion.  
Deployment is expressed as a clear prepare/install/configure/validate workflow and must be followed exactly.
