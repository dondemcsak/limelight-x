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
- **UI:** Avalonia‑based desktop application  
- **API server:** `/src/api`, started via `llx serve`. Bundled with UI (no separate deployment) — see `api.md`.

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
- **Step 4:** Confirm UI and bundled `/src/api` server binary are installed

## 4.3. Configure

- **Profiles:** Dev, Stage, Prod  
- **Configuration:**  
  - Backend URL per environment — must match the loopback address/port `llx serve` binds to (`127.0.0.1:4747` by default, see `api.md` §8)  
  - Log path per environment  
  - `ANTHROPIC_API_KEY` — **required**. The installer prompts for it on first launch if unset, and stores it via Windows Credential Manager; the UI sets it in the environment of the `llx serve` process it launches. The UI must not start `llx serve` without it, since `api.md` §8/§10 specifies the server refuses to start and exits immediately without this variable.  
  - Optional environment variables for advanced setups (e.g. `--port` override, passed to `llx serve` if the default port is unavailable)  
- **Selection:** Environment profile chosen at first launch or via config file

## 4.4. Validate

- **Step 1:** Launch Limelight‑X UI  
- **Step 2:** Confirm main window renders without errors  
- **Step 3:** Confirm environment profile is active (e.g., backend URL)  
- **Step 4:** Confirm `llx serve` started successfully; if `ANTHROPIC_API_KEY` is missing or the port is unavailable, the UI must surface the fatal startup error from `api.md` §8 rather than failing silently  
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
  - Logs  
  - Configuration files  
  - Caches  
- **Expectation:** System returns to pre‑install state

---

# 7. API Server Deployment

- **Bundling:** the `/src/api` server binary is bundled with the UI in the MSIX installer  
- **Location:** Installed alongside UI binaries  
- **Usage:** the UI launches it locally as `llx serve` (default `127.0.0.1:4747`, see `api.md` §8) when the UI starts, and terminates it when the UI exits; no separate deployment or service mode  
- **Secrets:** `ANTHROPIC_API_KEY` is supplied to the `llx serve` process via the environment, sourced from the value configured in §4.3

---

# Summary

This UI deployment specification defines Windows‑only, MSIX‑based installation of Limelight‑X with .NET prerequisites, environment profiles (Dev/Stage/Prod), semi‑automatic updates, full uninstall cleanup, and a bundled `/src/api` server launched locally via `llx serve`.  
Deployment is expressed as a clear prepare/install/configure/validate workflow and must be followed exactly.