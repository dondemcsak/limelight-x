# UI Accessibility

## Purpose
This document defines the accessibility requirements for the Limelight‑X Avalonia workflow dashboard.  
It specifies standards, keyboard navigation rules, focus management, screen reader semantics, inspector accessibility, color contrast, error accessibility, and interaction behaviors.  
This specification is authoritative.  
All implementation must follow these accessibility rules exactly.

Limelight‑X targets WCAG 2.2 AA — implementation must follow this spec directly, but conformance is **not independently verified for v0.1** (see §15) — and provides full keyboard navigation, exposes complete screen reader semantics, ensures deterministic focus behavior, and maintains high‑contrast visual indicators.  
Animations remain enabled, and basic keyboard shortcuts support core workflow actions.

---

# 1. Accessibility Standard Baseline

Limelight‑X targets **WCAG 2.2 AA** as its design baseline (see §15 on verification scope).

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
- Arrow keys — navigate tree views, inspectors, and sidebar  
- `Enter` — activate focused element  
- `Escape` — close inspectors or dismiss banners where applicable  

### Requirements
- All interactive elements must be reachable via keyboard.  
- No keyboard traps are permitted.  
- Keyboard order must follow visual order.  
- Sidebar navigation must be fully keyboard operable.

---

# 3. Focus Management

Focus moves to the **first interactive element** when:

- Navigating to a new page  
- Opening an inspector  
- Closing a modal dialog  
- Loading ExecutionPage after pipeline completion  

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

| Action   | Shortcut |
|----------|----------|
| Run      | Ctrl+R   |
| Explain  | Ctrl+E   |
| Trace    | Ctrl+T   |
| Save     | Ctrl+S   |

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

# 11. Editor Accessibility

The editor exposes **full ARIA textfield semantics**.

### Requirements
- Editor must expose role="textbox".  
- Editor must expose current line and column position.  
- Editor must expose validation errors via ARIA descriptions.  
- Editor must expose selection and cursor position.

---

# 12. Navigation Accessibility

Sidebar navigation uses **ARIA navigation roles**.

### Requirements
- Sidebar must expose role="navigation".  
- Each navigation item must expose role="link".  
- Current page must expose aria-current="page".  
- Keyboard navigation must follow visual order.

---

# 13. Inspector Collapse/Expand Accessibility

Inspector collapsible sections use **ARIA details/summary semantics**.

### Requirements
- Collapsible headers must expose role="button".  
- Expanded/collapsed state must be exposed via `aria-expanded`.  
- Summary text must be accessible.  
- Tree content must be accessible when expanded.

---

# 14. Focus Indicators

Focus indicators use a **high‑contrast white outline**.

### Requirements
- Focus outline must be clearly visible against dark backgrounds.  
- Focus outline must meet AA contrast.  
- Focus outline must not rely on color alone.  
- Focus outline must be consistent across all components.

---

# 15. Accessibility Testing

Limelight‑X does **not** require automated or manual accessibility testing for v0.1.

### Requirements
- Developers should follow this spec directly.  
- Accessibility compliance is achieved through deterministic implementation.  
- Future versions may introduce automated testing.

---

# Summary

Limelight‑X targets WCAG 2.2 AA as a design baseline (unverified for v0.1, see §15), and provides full keyboard navigation, deterministic focus management, complete screen reader semantics, and high‑contrast visual indicators.  
Inspector panels expose full semantic tree structures, error surfaces use ARIA alerts for modals, and the editor exposes full ARIA textfield semantics.  
Animations remain enabled, and basic keyboard shortcuts support core workflow actions.  
This accessibility model is deterministic and must be followed exactly.