# BDD UI Visual Regressions

> **Status: Deferred — out of scope for v0.1.**  
> `ui-testing.md` explicitly excludes snapshot/pixel‑diff tests from v0.1 scope, and no visual‑diff tooling is specified in `ui-build-pipeline.md` or `ui-testing.md`. The scenarios below are retained as a reference for a future revision, once a snapshot‑testing tool is selected and added to the testing spec, but must not be treated as active acceptance criteria for v0.1.
>
> **Superseded by the folder/tab workspace redesign.** The scenarios below still describe the retired HomePage/EditorPage/ExecutionPage/Sidebar layout (see `ui-architecture.md` §4, `ui-routing-navigation.md`, `ui-components.md` §3). They are left as-is rather than rewritten line-by-line, since this document is already non-authoritative for v0.1 — a future revision should replace "page" scenarios with Explorer/Tab Strip/Tab Content/Settings-modal equivalents at the same time a snapshot-testing tool is adopted.

## Purpose
This document defines Behavior‑Driven Development (BDD) scenarios for **visual regression stability** in Limelight‑X.  
It ensures that layout, spacing, component visibility, and structural rendering remain deterministic across versions.  
Scenarios use **pure Given/When/Then**, **medium granularity**, and cover **inline, banner, and modal error visuals**, **inspector expand/collapse + indentation**, and **sidebar/header/active state indicators**.

The document is organized **by page** (Home, Editor, Execution).

Color, typography, focus indicators, rendering errors, and persistence visuals are **not** included.

---

# 1. HomePage Visual Regression

## Scenario: HomePage layout remains stable
**Given** the user is on HomePage  
**When** the UI renders  
**Then** the primary layout grid appears in the same structure  
**And** spacing between sections remains unchanged  
**And** no components overlap

## Scenario: File selector remains visible and aligned
**Given** the user is on HomePage  
**When** the UI renders  
**Then** the file selector is visible  
**And** aligned to its designated grid position  
**And** padding around the selector remains unchanged

## Scenario: Global banner appears without shifting layout
**Given** the user is on HomePage  
**And** a global error banner is triggered  
**When** the banner appears  
**Then** the banner occupies its reserved top space  
**And** no other components shift vertically  
**And** spacing below the banner remains unchanged

## Scenario: Fatal modal appears centered without layout distortion
**Given** the user is on HomePage  
**And** a fatal error occurs  
**When** the modal dialog appears  
**Then** the modal is centered  
**And** the background layout remains unchanged  
**And** no components resize or shift

---

# 2. EditorPage Visual Regression

## Scenario: Editor layout remains stable
**Given** the user is on EditorPage  
**When** the UI renders  
**Then** the editor occupies its designated main column  
**And** spacing between editor and sidebar remains unchanged  
**And** no components overlap

## Scenario: Inline validation errors appear above editor without shifting content
**Given** the editor contains invalid CNL  
**When** inline validation errors appear  
**Then** the error block appears above the editor  
**And** the editor content remains in the same position  
**And** spacing below the error block remains unchanged

## Scenario: Global banner appears without affecting editor layout
**Given** the user is on EditorPage  
**And** a global error banner is triggered  
**When** the banner appears  
**Then** the banner occupies its reserved top space  
**And** the editor layout remains unchanged  
**And** no vertical compression occurs

## Scenario: Fatal modal appears centered without affecting editor layout
**Given** the user is on EditorPage  
**And** a fatal error occurs  
**When** the modal dialog appears  
**Then** the modal is centered  
**And** the editor layout remains unchanged  
**And** sidebar spacing remains unchanged

## Scenario: Sidebar active state indicator remains visually stable
**Given** the user is on EditorPage  
**When** the sidebar renders  
**Then** the Editor item shows its active indicator  
**And** spacing around the indicator remains unchanged  
**And** no other sidebar items shift

---

# 3. ExecutionPage Visual Regression

## Scenario: ExecutionPage layout remains stable
**Given** the user is on ExecutionPage  
**When** the UI renders  
**Then** inspectors appear in their designated positions  
**And** spacing between inspectors remains unchanged  
**And** no components overlap

## Scenario: Inspector expand/collapse indicator remains visually stable
**Given** the IR inspector is collapsed  
**When** the user expands the inspector  
**Then** the expand/collapse indicator changes state  
**And** indentation of tree nodes remains unchanged  
**And** spacing between nodes remains consistent

## Scenario: Inspector indentation remains stable across renders
**Given** the Raw AST inspector is expanded  
**When** the UI renders  
**Then** each tree node appears at its correct indentation level  
**And** spacing between levels remains unchanged  
**And** no nodes shift horizontally

## Scenario: Inline inspector errors appear without shifting inspector layout
**Given** the backend mock response includes an IR error  
**When** the IR inspector displays an inline error  
**Then** the error block appears above inspector content  
**And** inspector layout remains unchanged  
**And** spacing below the error block remains unchanged

## Scenario: Global banner appears without affecting inspector layout
**Given** the user is on ExecutionPage  
**And** a global error banner is triggered  
**When** the banner appears  
**Then** the banner occupies its reserved top space  
**And** inspector layout remains unchanged  
**And** no vertical compression occurs

## Scenario: Fatal modal appears centered without affecting inspector layout
**Given** the user is on ExecutionPage  
**And** a fatal error occurs  
**When** the modal dialog appears  
**Then** the modal is centered  
**And** inspector layout remains unchanged  
**And** spacing between inspectors remains unchanged

## Scenario: Sidebar active state indicator remains visually stable on ExecutionPage
**Given** the user is on ExecutionPage  
**When** the sidebar renders  
**Then** the Execution item shows its active indicator  
**And** spacing around the indicator remains unchanged  
**And** no other sidebar items shift

## Scenario: Page header remains visually stable across navigation
**Given** the user navigates between EditorPage and ExecutionPage  
**When** the header renders  
**Then** header spacing remains unchanged  
**And** header alignment remains unchanged  
**And** no components shift horizontally or vertically

---

# Summary

This BDD visual regression specification defines deterministic Given/When/Then scenarios for layout, spacing, component visibility, inspector indentation, sidebar active indicators, and error surface visuals across HomePage, EditorPage, and ExecutionPage.  
It excludes color, typography, focus indicators, rendering errors, and persistence visuals.  
Scenarios use medium granularity and behavioral naming.  
This visual regression model is authoritative and must be followed exactly.