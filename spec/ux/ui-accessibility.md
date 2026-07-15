# UI Accessibility

## Purpose
This document defines the accessibility requirements for the Limelight‑X Avalonia workflow dashboard.  
It specifies standards, keyboard navigation rules, focus management, screen reader semantics, inspector accessibility, color contrast, error accessibility, and interaction behaviors.  
This specification is authoritative.  
All implementation must follow these accessibility rules exactly.

Limelight‑X targets WCAG 2.2 AA — implementation must follow this spec directly, but conformance is **not independently verified for v0.1** (see §17) — and provides full keyboard navigation, exposes complete screen reader semantics, ensures deterministic focus behavior, and maintains high‑contrast visual indicators.  
Animations remain enabled, and basic keyboard shortcuts support core workflow actions.

---

# 1. Accessibility Standard Baseline

Limelight‑X targets **WCAG 2.2 AA** as its design baseline (see §17 on verification scope).

### Requirements
- All text must meet AA contrast requirements.  
- Decorative elements may use relaxed contrast.  
- Interactive elements must meet AA contrast.  
- Focus indicators must meet AA contrast.  
- All interactive components must be keyboard accessible.  
- All semantic roles must be exposed to assistive technologies.

---

# 2. Keyboard Navigation

Limelight‑X provides **full keyboard accessibility**.

### Supported Keys
- `Tab` — move forward through interactive elements  
- `Shift+Tab` — move backward  
- Arrow keys — navigate the folder tree, inspectors, and the tab strip  
- `Ctrl+Tab` / `Ctrl+Shift+Tab` — move to the next/previous open tab  
- `Enter` — activate focused element  
- `Escape` — close inspectors or dismiss banners where applicable  

### Requirements
- All interactive elements must be reachable via keyboard.  
- No keyboard traps are permitted.  
- Keyboard order must follow visual order.  
- The folder tree and tab strip must be fully keyboard operable.

### `Tab` Inside the CNL Editor Text Area (Editor‑Scoped Override)
Inside `CnlEditor`'s `TextArea`, `Tab` is intercepted by the editor before the app‑chrome "move focus" rule above applies (`bdd-ui-interactions.md` §2.19):
1. If `EditorViewModel.GhostSuggestion` is non‑null (an inline ghost‑text suggestion is showing at the caret — `bdd-ui-interactions.md` §2.18), `Tab` commits it via `ApplyQuickFixCommand` and the key event is marked handled — focus does **not** move.
2. Otherwise, `Tab` falls through unhandled to AvaloniaEdit's existing default behavior (indent insertion at the caret) — this predates this feature and is unchanged.
3. Focus‑navigation `Tab` (moving out of the editor to the next interactive element) only applies when neither of the above claims the key — i.e., in practice, a user must use `Shift+Tab`, click, or another navigation method to leave the editor via keyboard, consistent with how any code‑editor `TextArea` (including AvaloniaEdit's own default indent behavior) already claims `Tab` for itself rather than yielding to focus navigation.

---

# 3. Focus Management

Focus moves to the **first interactive element** when:

- Opening or switching to a tab  
- Opening an inspector  
- Closing a modal dialog  
- A tab's execution panel finishes streaming (`final_result_ready`/`pipeline_failed`), if that tab is currently active  

### Requirements
- Focus must never be lost or placed on non‑interactive elements.  
- Focus movement must be deterministic and predictable.  
- Focus indicators must be clearly visible.

---

# 4. Screen Reader Support

Limelight‑X provides **full screen reader semantics**, including:

- ARIA roles  
- ARIA labels  
- ARIA descriptions  
- ARIA live regions (where applicable)  

### Requirements
- All interactive elements must have accessible names.  
- All structural elements must have correct roles.  
- All inspectors must expose semantic structure.  
- Error surfaces must expose ARIA alerts.

---

# 5. Inspector Accessibility

Inspector panels (AST, Normalized AST, IR, Prompts, Model Outputs) expose **full semantic tree structures** to screen readers.

### Requirements
- AST nodes must be represented as hierarchical tree items.  
- IR operations must be represented as structured lists with metadata.  
- Prompt blocks must expose operation index and metadata.  
- Model outputs must expose content type and parsed structure.  
- Collapsible sections must expose expanded/collapsed state.

---

# 6. Syntax Highlighting Accessibility

Syntax highlighting does **not** require special accessibility handling.

### Requirements
- Screen readers read plain text content.  
- Highlighting colors must meet AA contrast requirements.  
- No semantic meaning may rely solely on color.

---

# 7. Color Contrast

Limelight‑X uses a neon‑dark aesthetic but must maintain:

- **Strict WCAG AA contrast for all body text**  
- **Relaxed contrast for decorative elements only**

### Requirements
- Text contrast ≥ 4.5:1  
- Large text contrast ≥ 3:1  
- Icons conveying meaning must meet AA contrast  
- Lime accent must meet AA contrast when used for focus indicators

---

# 8. Reduced Motion

Limelight‑X does **not** provide a reduced‑motion mode.

### Requirements
- Deterministic animations remain enabled.  
- Animations must be minimal and non‑distracting.  
- No flashing or strobing effects are permitted.

---

# 9. Keyboard Shortcuts

Limelight‑X provides **basic keyboard shortcuts**:

| Action        | Shortcut |
|---------------|----------|
| Run           | Ctrl+R   |
| Explain       | Ctrl+E   |
| New LLX File  | Ctrl+N   |
| New TXT File  | — (menu only, no shortcut) |
| Open File     | Ctrl+O   |
| Open Folder   | Ctrl+K, Ctrl+O |
| Save          | Ctrl+S   |
| Save As       | Ctrl+Shift+S |
| Save All      | Ctrl+K, S |
| Settings      | Ctrl+,   |
| About         | — (menu only, no shortcut) |
| Close Tab     | Ctrl+W   |

There is no Trace shortcut — the Trace trigger is removed entirely (see `ui-viewmodels.md` §6). Run and Explain act on the active tab.  
Save is now wired to `WorkspaceViewModel.SaveCommand` (`ui-viewmodels.md` §3) — previously documented here but unimplemented.  
`Ctrl+K` is a two‑key chord prefix shared by two distinct completions: `Ctrl+K, Ctrl+O` (Open Folder) and `Ctrl+K, S` (Save All). Both reuse the same chord‑handling mechanism (arm on `Ctrl+K`, complete on the next keypress within the existing timeout window).

### Requirements
- Shortcuts must not conflict with OS‑level shortcuts.  
- Shortcuts must be discoverable via a help tooltip or menu.  
- Shortcuts must be accessible via keyboard and screen reader.

---

# 10. Error Accessibility

Error surfaces must expose ARIA semantics:

- **Modals use ARIA alerts**  
- Inline errors use ARIA descriptions  
- Global banners do not use live regions  

### Requirements
- Modal dialogs must announce themselves immediately.  
- Inline errors must be associated with the component they describe.  
- Error codes must be included in accessible descriptions.

---

# 11. Editor & Form Accessibility

The editor exposes **full ARIA textfield semantics**.

### Requirements
- Editor must expose role="textbox".  
- Editor must expose current line and column position.  
- Editor must expose validation errors via ARIA descriptions.  
- Editor must expose selection and cursor position.

### PlainTextEditor
The generic text editor used for non‑`.llx` tabs (`ui-components.md` §4.3) follows the same base pattern as the CNL editor above — role="textbox", exposed cursor/selection position — but without CNL‑specific ARIA descriptions (no syntax error descriptions, since it has no validation).

### Settings Form Fields
`TextField`, `SecureTextField`, and `SelectField` (`ui-components.md` §5.6–5.8) reuse the same pattern:
- Each field must expose an accessible label (role="textbox" for `TextField`/`SecureTextField`, appropriate combobox role for `SelectField`).  
- Validation errors must be exposed via ARIA descriptions, the same mechanism the editor uses.  
- `SecureTextField`'s show/hide toggle must expose an accessible name ("Show API key" / "Hide API key") and must not rely on the icon alone.  
- Tab order follows visual order: Port → Log Path → API Key → Environment Profile → Save/Cancel.

---

# 12. Workspace Accessibility (File Tree & Tabs)

The folder tree and tab strip (`ui-components.md` §3.1–3.2) use **ARIA tree and tab roles**, replacing the previous Sidebar's navigation-role pattern.

### File Tree Requirements
- The file tree container must expose role="tree".  
- Each file/folder entry must expose role="treeitem".  
- Folder nodes must expose `aria-expanded` (true/false) reflecting `FileTreeNodeViewModel.IsExpanded`.  
- The currently open (focused) file's tree node must expose `aria-selected="true"`.  
- Keyboard navigation must follow visual (hierarchical) order.

### Tab Strip Requirements
- The tab strip container must expose role="tablist".  
- Each tab must expose role="tab" with `aria-selected` reflecting whether it is `WorkspaceViewModel.ActiveTab`.  
- Each tab's content area must expose role="tabpanel", associated with its tab via `aria-labelledby`.  
- Each tab's close button must expose an accessible name of "Close `<filename>`" — the icon alone must not carry this meaning.  
- Tab dirty state must be exposed accessibly (e.g. via the accessible name, not by color/dot alone).

### Settings Gear Icon
The gear icon (`ui-components.md` §3.4) exposes an accessible name of "Open Settings" since it has no visible text label, and exposes its disabled state (via the standard disabled-control semantics) while `IExecutionLockService.IsAnyExecutionRunning == true`.

---

# 13. MenuBar & Modal Accessibility

### MenuBar Requirements
- The MenuBar (`ui-components.md` §3.5) must expose role="menubar"; each top‑level menu exposes role="menu"; each item exposes role="menuitem".  
- File and Help must be reachable via `Alt+F` / `Alt+H` mnemonics, in addition to `Tab`/arrow‑key navigation.  
- Arrow keys navigate within an open menu and between top‑level menus; `Escape` closes an open menu and returns focus to the MenuBar.  
- Disabled items (e.g. Save with no active tab, Settings during execution) expose the standard disabled‑control semantics, the same pattern as the Settings Gear Icon above.  
- Each item's exposed keyboard shortcut hint must match the `ui-styling-theming.md`/§9 shortcuts table exactly — no divergent or stale hints.

### About Modal Requirements
- The About modal announces itself immediately on open, using the same ARIA‑alert pattern as other modals (§10).  
- Closing the About modal returns focus to the first interactive element, the same as any modal (§3).  
- The GitHub link must expose an accessible name indicating it opens externally (e.g. "Limelight‑X on GitHub (opens in browser)"), not a bare URL.

---

# 14. Inspector Collapse/Expand Accessibility

Inspector collapsible sections use **ARIA details/summary semantics**.

### Requirements
- Collapsible headers must expose role="button".  
- Expanded/collapsed state must be exposed via `aria-expanded`.  
- Summary text must be accessible.  
- Tree content must be accessible when expanded.
- All six inspector panels (`ui-components.md` §5) are always present in the accessibility tree from the moment the tab opens — `aria-expanded` toggles between `false`/`true` as panels auto-expand or are manually collapsed/expanded; panels are never inserted into or removed from the tree.

---

# 15. SplitterControl Accessibility

The `SplitterControl` (`ui-components.md` §4.5) — used for the editor/panel split and each panel's resize handle — exposes:

- role="separator", with `aria-orientation="horizontal"` (both usages divide vertically stacked regions).
- `aria-valuenow`/`aria-valuemin`/`aria-valuemax` reflecting the current split position (`EditorPaneRatio` for the editor/panel splitter, or the panel's `Height` within its allowed range for a panel handle).
- Keyboard resizability: when focused, `ArrowUp`/`ArrowDown` adjust the split by a fixed step, matching the same resize effect as a mouse drag.
- An accessible name identifying what it resizes (e.g. "Resize editor and results panel", "Resize Raw AST panel").

### Requirements
- The `SplitterControl` must be reachable via `Tab`/`Shift+Tab`, consistent with §2 Keyboard Navigation.  
- Resizing via keyboard must be deterministic and produce the same end state as an equivalent mouse drag.

---

# 16. Focus Indicators

Focus indicators use a **high‑contrast white outline**.

### Requirements
- Focus outline must be clearly visible against dark backgrounds.  
- Focus outline must meet AA contrast.  
- Focus outline must not rely on color alone.  
- Focus outline must be consistent across all components.

---

# 17. Accessibility Testing

Limelight‑X does **not** require automated or manual accessibility testing for v0.1.

### Requirements
- Developers should follow this spec directly.  
- Accessibility compliance is achieved through deterministic implementation.  
- Future versions may introduce automated testing.

---

# Summary

Limelight‑X targets WCAG 2.2 AA as a design baseline (unverified for v0.1, see §17), and provides full keyboard navigation, deterministic focus management, complete screen reader semantics, and high‑contrast visual indicators.  
Inspector panels expose full semantic tree structures, error surfaces use ARIA alerts for modals, and the editor exposes full ARIA textfield semantics.  
The MenuBar and About modal follow the same keyboard, ARIA, and focus‑management patterns as the rest of the workspace.  
Animations remain enabled, and basic keyboard shortcuts support core workflow actions.  
This accessibility model is deterministic and must be followed exactly.