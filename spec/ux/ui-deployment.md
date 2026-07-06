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

- **Primary distribution:** MSIX installer only  
- **Channel:** Stable releases only  
- **Source:** Published artifacts from the build pipeline (GitHub Releases or equivalent)

---

# 3. Installation Requirements

- **Prerequisite:** .NET runtime must be installed  
- **Installer:** MSIX checks for .NET; if missing, installation fails with a clear message  
- **Environment:** Windows 10 or later

---

# 4. Deployment Stages

## 4.1. Prepare

- **Step 1:** Obtain latest stable MSIX installer  
- **Step 2:** Verify installer origin (trusted release channel)  
- **Step 3:** Ensure .NET runtime is installed  
- **Step 4:** Confirm user has Windows 10+ with required permissions

## 4.2. Install

- **Step 1:** Run MSIX installer interactively  
- **Step 2:** Accept license and terms  
- **Step 3:** Complete installation to default path  
- **Step 4:** Confirm `LimelightX.exe` and bundled `llx.exe` are installed

## 4.3. Configure

- **Profiles:** Dev, Stage, Prod  
- **Configuration file:** `%APPDATA%\LimelightX\config.json`, containing:
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

- **Persistent log file:** `LogPath` names a **directory**, not a file — the log file within it is always named `Limelight-x-log.txt`. An empty/unset `LogPath` (the default) resolves to `config.json`'s own directory, i.e. `%APPDATA%\LimelightX\Limelight-x-log.txt`; a custom `LogPath` writes to `<LogPath>\Limelight-x-log.txt` instead. This resolution happens at the moment logging is configured — an empty `LogPath` is never rewritten to a concrete value in `config.json`.  
  - **Mechanism:** the UI logs through `Microsoft.Extensions.Logging`'s `ILogger`/`ILoggerFactory`/`LogLevel` abstractions; Serilog (`Serilog.Extensions.Logging` + `Serilog.Sinks.File`) is the configured provider/file sink (see `CLAUDE.md` §3.5's approved-dependency list).  
  - **Retention:** the file is opened in append mode and never truncated — entries accumulate across app restarts indefinitely (no rotation in v0.1).  
  - **Format:** plain text, one line per entry: `[<UTC ISO-8601 timestamp>] [<LogLevel>] <Code>: <Message>`, with `(line L, column C)` appended when the error has a location, and the error's category included in the message. Example: `[2026-07-04T18:22:31Z] [Error] ERR_CNL_PARSE: Missing period. (Category=Pipeline)`.  
  - **Severity mapping:** `UiError.Severity` → `LogLevel`: `Info`→`Information`, `Warning`→`Warning`, `Error`→`Error`, `Fatal`→`Critical`.  
  - **Failure safety:** a failure to create the log directory or write to the log file must never surface as a user-facing error, crash the app, or block any other functionality — it fails silently. See `ui-error-handling.md` for what gets logged.
- **Selection:** Environment profile chosen via `config.json` at any time, or edited live via the **in-app Settings modal** (`ui-viewmodels.md` §9, `ui-routing-navigation.md` §8) — the Settings modal edits the same `config.json` file plus Credential Manager, and applies changes by restarting `llx serve` in the background.

## 4.4. Validate

- **Step 1:** Launch Limelight‑X UI  
- **Step 2:** Confirm main window renders without errors  
- **Step 3:** Confirm environment profile is active (e.g., backend port)  
- **Step 4:** Confirm `llx serve` started successfully. If `config.json` is missing/invalid or `ANTHROPIC_API_KEY` is unset (first launch, or a broken config), `LimelightX.exe` auto‑opens the Settings modal on launch instead of restoring the last workspace. The Explorer and Tab Strip remain fully usable in this state (browsing folders and opening tabs needs no backend), but Run/Explain stay disabled on every `.llx` tab until the user saves valid Settings (see `ui-routing-navigation.md` §9). This is the only first-run experience; there is no separate onboarding page or installer-hosted wizard.  
- **Step 5:** Optionally run a simple pipeline to confirm connectivity

---

# 5. Update Strategy

- **Model:** Semi‑automatic via MSIX auto‑update  
- **Behavior:**  
  - MSIX checks for newer stable versions  
  - User is prompted to update  
  - Update preserves configuration and environment profiles

---

# 6. Uninstall Behavior

- **Mode:** Full cleanup  
- **Uninstall removes:**  
  - Application binaries  
  - Logs — the file at whatever `LogPath` resolves to per §4.3 (`%APPDATA%\LimelightX\Limelight-x-log.txt` by default)  
  - Configuration files  
  - Caches  
- **Expectation:** System returns to pre‑install state

---

# 7. API Server Deployment

- **Bundling:** `llx.exe` is bundled with `LimelightX.exe` in the MSIX installer  
- **Location:** Installed alongside the UI binary  
- **Usage:** `LimelightX.exe` launches `llx.exe serve` locally (default `127.0.0.1:4747`, see `api.md` §8) when the UI starts, and terminates it when the UI exits; no separate deployment or service mode  
- **Secrets:** `ANTHROPIC_API_KEY` is supplied to the `llx.exe serve` process via the environment, sourced from the value configured in §4.3

---

# Summary

This UI deployment specification defines Windows‑only, MSIX‑based installation of `LimelightX.exe` with .NET prerequisites, environment profiles (Dev/Stage/Prod), semi‑automatic updates, full uninstall cleanup, and a bundled `llx.exe` server launched locally via `llx.exe serve`.  
Deployment is expressed as a clear prepare/install/configure/validate workflow and must be followed exactly.