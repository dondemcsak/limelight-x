# UI Testing

## Purpose
This document defines the **unit‑testing strategy** for the Limelight‑X UI.  
It specifies how the UI is tested using the **.NET Avalonia.Headless test harness with xUnit**, with **pure mock backend responses**, **basic test isolation**, and **workflow‑organized test suites**.  
The testing scope is intentionally narrow and deterministic: **unit tests only**, with limited inspector, navigation, and workflow coverage.

`/src/api` (Rust) is tested separately per `spec/bdd-api.md`, using `cargo test` and a mock model adapter as described in `spec/bdd.md`'s testing rules — not covered by this document.

The document is organized by **workflow**:
- Load  
- Edit  
- Run  
- Explain  
- Trace  

---

# 1. Testing Overview

Limelight‑X UI testing includes:
- **Unit tests only**  
- **.NET Avalonia.Headless test harness with xUnit**  
- **Pure mock backend responses** (mocked `PipelineService` HTTP client, no real `/src/api` calls)  
- **Basic isolation (reset UI state per test)**  
- **No snapshot tests**  
- **No performance tests**  
- **No concurrency tests**  
- **No accessibility tests**

Test coverage includes:
- Validation errors  
- Sidebar + workflow navigation  
- Inspector expand/collapse  
- Deterministic Run/Explain/Trace workflows  

---

# 2. Load Workflow Tests

## 2.1. Valid File Load
- Load a valid `.llx` file  
- Assert editor receives correct content  
- Assert navigation to EditorPage succeeds  
- Assert no validation errors appear

## 2.2. Invalid File Load
- Load an invalid `.llx` file  
- Assert inline error appears  
- Assert UI remains on HomePage  
- Assert no backend calls occur

## 2.3. File Load State Reset
- Load a file  
- Reset UI state  
- Assert editor is empty  
- Assert no residual validation errors remain

---

# 3. Edit Workflow Tests

## 3.1. Valid CNL Editing
- Insert valid CNL  
- Assert no validation errors  
- Assert Run/Explain/Trace buttons enabled

## 3.2. Invalid CNL Editing
- Insert invalid CNL  
- Assert inline validation errors appear  
- Assert Run/Explain/Trace buttons disabled

## 3.3. Validation Persistence
- Insert invalid CNL  
- Navigate away  
- Navigate back  
- Assert validation errors persist

## 3.4. Editor State Reset
- Insert text  
- Reset UI state  
- Assert editor is empty  
- Assert no validation errors remain

---

# 4. Run Workflow Tests

## 4.1. Successful Run
- Insert valid CNL  
- Mock backend success  
- Trigger Run  
- Assert ExecutionPage loads  
- Assert final result inspector appears

## 4.2. Validation Blocks Run
- Insert invalid CNL  
- Trigger Run  
- Assert inline validation errors  
- Assert no backend calls occur  
- Assert UI remains on EditorPage

## 4.3. Run State Reset
- Execute pipeline  
- Reset UI state  
- Assert ExecutionPage is cleared  
- Assert no inspectors remain visible

---

# 5. Explain Workflow Tests

## 5.1. Successful Explain
- Insert valid CNL  
- Mock backend AST success  
- Trigger Explain  
- Assert Raw AST + Normalized AST inspectors appear

## 5.2. Validation Blocks Explain
- Insert invalid CNL  
- Trigger Explain  
- Assert inline validation errors  
- Assert UI remains on EditorPage

## 5.3. Explain State Reset
- Execute Explain  
- Reset UI state  
- Assert inspectors cleared  
- Assert no AST nodes remain

---

# 6. Trace Workflow Tests

## 6.1. Successful Trace
- Insert valid CNL  
- Mock backend IR + prompts + model outputs  
- Trigger Trace  
- Assert IR, Prompts, Model Outputs inspectors appear

## 6.2. Validation Blocks Trace
- Insert invalid CNL  
- Trigger Trace  
- Assert inline validation errors  
- Assert UI remains on EditorPage

## 6.3. Trace State Reset
- Execute Trace  
- Reset UI state  
- Assert inspectors cleared  
- Assert no IR or prompt nodes remain

---

# 7. Navigation Tests

## 7.1. Sidebar Navigation to EditorPage
- Load file  
- Navigate via sidebar  
- Assert EditorPage loads  
- Assert editor content visible

## 7.2. Sidebar Navigation to ExecutionPage
- Execute pipeline  
- Navigate via sidebar  
- Assert ExecutionPage loads  
- Assert inspectors visible

## 7.3. Workflow Navigation
- Trigger Run/Explain/Trace  
- Assert automatic navigation to ExecutionPage  
- Assert correct inspectors appear

---

# 8. Inspector Tests

## 8.1. Expand Inspector
- Execute pipeline  
- Expand IR inspector  
- Assert tree becomes visible  
- Assert indentation correct

## 8.2. Collapse Inspector
- Expand inspector  
- Collapse inspector  
- Assert tree becomes hidden

## 8.3. Inspector Reset
- Expand inspector  
- Reset UI state  
- Assert inspector collapsed  
- Assert no nodes visible

---

# Summary

This UI testing specification defines deterministic unit‑test coverage for Limelight‑X using the .NET Avalonia.Headless test harness with xUnit.  
It covers validation, navigation, inspector behavior, and deterministic Run/Explain/Trace workflows.  
Tests use pure mock backend responses, basic isolation, and workflow‑organized suites.  
This testing model is authoritative and must be followed exactly.