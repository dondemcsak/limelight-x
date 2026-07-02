# UI Styling & Theming

## Purpose
This document defines the complete styling and theming system for the Limelight‑X Avalonia workflow dashboard.  
It specifies the color palette, typography, component styling, iconography, animations, and branding rules.  
This specification is authoritative.  
All implementation must follow this styling system exactly.

The UI embodies the **Limelight** identity:  
a dark, neon‑accented, spotlight‑inspired aesthetic that supports analysts and citizen developers while maintaining a clean, deterministic workflow dashboard feel.

---

# 1. High‑Level Styling Overview

Limelight‑X uses a **dark neon aesthetic** with a **neon‑lime accent**, inspired by stage lighting and the “limelight” metaphor.

Core characteristics:

- Dark background  
- Neon lime accent used **sparingly** for highlights and active states  
- Soft shadows and subtle depth  
- Compact layout density  
- VS Code‑style syntax highlighting  
- Fluent UI iconography  
- Strong branding in header and key workflow areas  
- Deterministic animations (100–150ms)  
- Fixed theme system for v0.1 (no extensibility)

Light mode exists but is secondary; dark mode is the default.

---

# 2. Color Palette

## 2.1 Base Colors (Dark Theme)
```
BackgroundPrimary: #0D0D0D
BackgroundSecondary: #1A1A1A
Surface: #1F1F1F
SurfaceHover: #262626
Border: #333333
TextPrimary: #E5E5E5
TextSecondary: #B3B3B3
TextMuted: #808080
```

## 2.2 Accent Colors (Neon Lime)
Accent is used **sparingly** for active states, selection, and key workflow highlights.

```
AccentPrimary: #10B981   // Neon green (base)
AccentGlow: #32FF9C      // Brighter lime for glow effects
AccentMuted: #0A8F63     // Darker lime for subtle states
```

## 2.3 Error & Status Colors
```
Error: #EF4444
Warning: #F59E0B
Info: #3B82F6
Success: #10B981
```

## 2.4 Light Theme (Secondary)
Light theme mirrors dark theme values but uses muted lime instead of neon.

```
BackgroundPrimary: #FFFFFF
BackgroundSecondary: #F5F5F5
Surface: #FAFAFA
Border: #DDDDDD
TextPrimary: #1A1A1A
TextSecondary: #4D4D4D
AccentPrimary: #10B981
```

---

# 3. Typography

Limelight‑X uses a dual‑font system:

### UI Font
```
Inter (Regular, Medium, SemiBold)
```

### Code & Editor Font
```
JetBrains Mono (Regular, Medium)
```

### Rules
- All inspector panels use Inter.  
- Editor uses JetBrains Mono exclusively.  
- No font substitutions.  
- Line height must be consistent and deterministic.

---

# 4. Component Styling

## 4.1 General Component Style
Limelight‑X uses **soft shadows + subtle depth**:

```
ShadowSmall: 0px 1px 3px rgba(0,0,0,0.4)
ShadowMedium: 0px 4px 8px rgba(0,0,0,0.35)
BorderRadius: 6px
BorderColor: #333333
```

### Rules
- No heavy shadows.  
- No flat design.  
- No high‑contrast borders except error states.  
- Depth must be subtle and deterministic.

---

## 4.2 Buttons
Primary buttons use **accent sparingly**:

```
Background: Surface
Border: AccentPrimary
Text: AccentPrimary
Hover: SurfaceHover
ActiveGlow: AccentGlow (subtle outer glow)
```

Secondary buttons:

```
Background: Surface
Border: BorderColor
Text: TextPrimary
Hover: SurfaceHover
```

Disabled buttons:

```
Background: #1A1A1A
Text: #555555
Border: #2A2A2A
```

---

## 4.3 Inspector Panels
Inspector panels use **card‑style surfaces**:

```
Background: Surface
Border: BorderColor
Shadow: ShadowSmall
HeaderText: TextPrimary
HeaderAccent: AccentPrimary (only when active)
```

### Collapse/Expand
- Chevron icon from Fluent UI  
- Minimal deterministic animation (100–150ms)  
- No bounce, overshoot, or easing curves beyond linear or ease‑out

---

## 4.4 Tree Views (AST Panels)
Tree views follow **VS Code dark theme**:

- Chevron expanders  
- Indentation guides  
- Syntax‑colored node text  
- Hover highlight: `SurfaceHover`  
- Selected node: subtle lime border (`AccentMuted`)

---

## 4.5 IR Operation Cards
Operation cards use:

```
Background: Surface
Border: BorderColor
Shadow: ShadowSmall
HeaderAccent: AccentPrimary (only for active operation)
SyntaxHighlighting: VS Code Dark+
```

---

## 4.6 Prompt & Model Output Blocks
Rendered using:

- VS Code Dark+ syntax highlighting  
- Markdown styling  
- Table rendering with alternating row backgrounds  
- JSON syntax coloring  
- No neon accents except for active selection

---

# 5. Syntax Highlighting

Limelight‑X uses **VS Code Dark+** as the base theme.

### CNL Highlighting Rules
- Keywords: `#569CD6`  
- Pronouns: `#4EC9B0`  
- Resources: `#CE9178`  
- Expression holes: `#C586C0`  
- Strings: `#D69D85`  
- Errors: `#EF4444` underline  
- Editor cursor: `AccentPrimary` (thin lime bar)

---

# 6. Iconography

Limelight‑X uses **Fluent UI icons** exclusively.

### Rules
- Chevron icons for collapsible panels  
- Fluent folder/file icons for file loader  
- Fluent run/explain/trace icons  
- No Material icons  
- No custom SVGs in v0.1  

---

# 7. Animations

Animations must be:

- deterministic  
- minimal  
- 100–150ms duration  
- linear or ease‑out only  
- no bounce, wobble, or spring physics

### Animated Elements
- Collapse/expand panels  
- Hover transitions  
- Button active glow  

### Non‑Animated Elements
- Tree view expanders  
- Syntax highlighting  
- Editor cursor  
- Error banners

---

# 8. Branding

Limelight‑X uses **strong branding**:

### Branding Elements
- Neon lime accent  
- Limelight‑X logo in title bar  
- Branded header bar  
- Subtle spotlight/glow motif behind header  
- Accent used sparingly to avoid overwhelming the UI

### Rules
- Branding must not interfere with readability  
- Glow effects must be subtle  
- No animated branding  
- No background images behind content

---

# 9. Layout Density

Limelight‑X uses a **compact developer‑style layout**:

```
PaddingSmall: 4px
PaddingMedium: 8px
PaddingLarge: 12px
PanelSpacing: 8px
EditorLineHeight: 1.35
```

### Rules
- No excessive whitespace  
- Panels must be tightly stacked  
- Editor must feel like a code tool, not an enterprise dashboard

---

# 10. Theme Extensibility

v0.1 uses a **fixed theme**:

- No theme packs  
- No user‑defined palettes  
- No runtime theme switching beyond light/dark  
- No custom accent colors  

Future versions may introduce extensibility.

---

# Summary

Limelight‑X uses a dark neon aesthetic with a sparingly applied neon‑lime accent, embodying the “limelight” identity.  
The UI is compact, developer‑friendly, and deterministic, with VS Code Dark+ syntax highlighting, Fluent UI icons, soft shadows, and subtle depth.  
Inspector panels use card‑style surfaces, AST panels use VS Code‑style tree views, and all animations are minimal and deterministic.  
This styling system is fixed for v0.1 and must be followed exactly.