---
name: FAP
description: Zero-setup LAN file sharing — Fluent desktop shell with a thin guest web browser.
colors:
  ink-slate: "#1b1f24"
  cool-paper: "#f6f7f9"
  mist-header: "#eef1f6"
  card-white: "#ffffff"
  border-cool: "#d7dde5"
  muted-slate: "#5c6570"
  system-blue: "#0b57d0"
  slot-green: "#0f7b3a"
  slot-red: "#b42318"
  ink-slate-dark: "#e8eaed"
  cool-paper-dark: "#121417"
  mist-header-dark: "#1f242b"
  card-dark: "#1b1f24"
  border-dark: "#2f363e"
  muted-dark: "#9aa3ad"
  system-blue-dark: "#8ab4ff"
typography:
  display:
    fontFamily: "Segoe UI Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.75rem"
    fontWeight: 600
    lineHeight: 1.25
    letterSpacing: "normal"
  headline:
    fontFamily: "Segoe UI Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "normal"
  title:
    fontFamily: "Segoe UI Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 600
    lineHeight: 1.35
    letterSpacing: "normal"
  body:
    fontFamily: "Segoe UI Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.45
    letterSpacing: "normal"
  label:
    fontFamily: "Segoe UI Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "0.85rem"
    fontWeight: 400
    lineHeight: 1.35
    letterSpacing: "0.04em"
rounded:
  sm: "8px"
  md: "10px"
spacing:
  xs: "8px"
  sm: "12px"
  md: "16px"
  lg: "24px"
  page-x: "1.25rem"
  page-y: "1.5rem"
components:
  guest-breadcrumb:
    backgroundColor: "{colors.mist-header}"
    textColor: "{colors.system-blue}"
    rounded: "{rounded.sm}"
    padding: "0.75rem 1rem"
  guest-table-wrap:
    backgroundColor: "{colors.card-white}"
    textColor: "{colors.ink-slate}"
    rounded: "{rounded.md}"
    padding: "0.5rem"
  guest-link:
    backgroundColor: "transparent"
    textColor: "{colors.system-blue}"
  guest-link-hover:
    backgroundColor: "transparent"
    textColor: "{colors.system-blue}"
  winui-page:
    backgroundColor: "transparent"
    textColor: "{colors.ink-slate}"
    padding: "16px"
  winui-status:
    backgroundColor: "transparent"
    textColor: "{colors.ink-slate}"
    typography: "{typography.title}"
---

# Design System: FAP

## 1. Overview

**Creative North Star: "The LAN Console"**

FAP’s visual system is a status-first mesh console: restrained Fluent utility with readable density and chrome that never outranks connection, peers, or transfers. The desktop client is the primary surface — WinUI 3, Mica shell, NavigationView — patterned after Microsoft PowerToys Settings ([`src/settings-ui`](https://github.com/microsoft/PowerToys/tree/main/src/settings-ui)). Guest web is a thin share desk: cool neutrals, one system blue for links, semantic greens/reds for slot state, and a capable directory table — not a second full client.

Personality is **fast, plain, capable**. One Segoe family carries everything. Accent color marks actions, selection, and state — never decoration. Depth is a Fluent hybrid: shell materials and system elevation on desktop; flat bordered panels and tonal steps on guest web.

This system explicitly rejects legacy dense P2P clients — cluttered toolbars, tiny fonts, every control visible at once — and rejects consumer “drive” marketing chrome.

**Key Characteristics:**
- Status before chrome
- PowerToys Settings structure on desktop; thin guest table on web
- Cool Fluent neutrals + System Blue accent ≤10%
- Flat guest surfaces; Mica/shell depth only where the OS provides it
- WCAG 2.2 AA target on guest web; automation-friendly WinUI

## 2. Colors

Cool Fluent neutrals with a single system-blue interactive accent and semantic slot colors. Light tokens are canonical for guest CSS variables; dark mirrors follow `prefers-color-scheme`.

### Primary
- **System Blue** (#0b57d0): Links, interactive text, row-hover tint on guest web. Dark: **System Blue Dark** (#8ab4ff). Desktop selection/accent follows the Windows accent via Fluent theme resources — do not invent a second brand blue.

### Secondary
- **Slot Green** (#0f7b3a): Free upload slots / positive transfer state (`.green`).
- **Slot Red** (#b42318): Exhausted slots / error state (`.red`).

### Neutral
- **Ink Slate** (#1b1f24): Primary body text (light). Dark ink: **Ink Slate Dark** (#e8eaed).
- **Cool Paper** (#f6f7f9): Page background (light). Dark: **Cool Paper Dark** (#121417).
- **Mist Header** (#eef1f6): Breadcrumb / header strip (light). Dark: **Mist Header Dark** (#1f242b).
- **Card White** (#ffffff): Table and panel fill (light). Dark: **Card Dark** (#1b1f24).
- **Border Cool** (#d7dde5): 1px structural borders (light). Dark: **Border Dark** (#2f363e).
- **Muted Slate** (#5c6570): Secondary copy, table headers, hints (light). Dark: **Muted Dark** (#9aa3ad).

### Named Rules
**The One Accent Rule.** System Blue (and OS accent on desktop) is used for interactive affordances and selection only — ≤10% of any screen. Neutrals carry the rest.

**The Slot Signal Rule.** Green and red mean upload-slot / transfer truth, never decorative badges or marketing highlights.

## 3. Typography

**Display Font:** Segoe UI Variable (with Segoe UI, system-ui)
**Body Font:** Segoe UI Variable (with Segoe UI, system-ui)
**Label/Mono Font:** Same family — no separate display or mono stack for UI chrome

**Character:** One technical sans at multiple weights. Familiar Windows density; no display pairing, no fluid hero scales.

### Hierarchy
- **Display** (600, 1.75rem / ~28px, ~1.25): Guest share title (`.guest-header h1`).
- **Headline** (600, 1.25rem / 20px, ~1.3): Settings / feature page titles on WinUI.
- **Title** (600, 1.125rem / 18px, ~1.35): Connection status and section emphasis (`home.status`).
- **Body** (400, 1rem, 1.45): Primary UI copy; guest notes ~0.95rem where secondary.
- **Label** (400, 0.85rem, letter-spacing 0.04em, uppercase on guest table headers): Column headers and compact meta; WinUI secondary lines often 12px at ~0.75 opacity.

### Named Rules
**The One Family Rule.** Never introduce a second typeface for UI labels, buttons, or data. Segoe (or Fluent theme fonts) only.

**The Fixed Scale Rule.** Product UI uses fixed rem/px steps, not clamp() display sizes that shrink awkwardly in panes.

## 4. Elevation

Fluent hybrid: the desktop shell may use Mica and system control elevation; guest web is flat by default — depth comes from background steps (Cool Paper → Mist Header → Card White) and 1px Border Cool outlines. No soft wide drop shadows on guest cards or tables.

### Shadow Vocabulary
- **None on guest web** — borders and tonal layers only.
- **Desktop system** — rely on WinUI / Fluent default control chrome and `MicaBackdrop`; do not add custom multi-layer decorative shadows.

### Named Rules
**The Flat Guest Rule.** Guest surfaces stay flat at rest. If a panel needs separation, use border + surface step, not `box-shadow` with blur ≥16px.

**The Shell Depth Rule.** Real depth belongs to the OS shell (Mica, NavigationView, TitleBar). Content pages stay calm and unshadowed.

## 5. Components

Refined and restrained: standard Fluent controls on desktop; thin guest directory listing on web. Prefer PowerToys Settings patterns for settings and navigable sections.

### Buttons
- **Shape:** Fluent default corner radius (system); guest has no custom button chrome — links are text.
- **Primary:** WinUI `Button` with theme resources; Save/Cancel in settings rows with ~12px top margin rhythm.
- **Hover / Focus:** System Fluent states; guest links underline on hover. Focus rings must remain visible for WCAG.

### Cards / Containers
- **Corner Style:** Gently curved — breadcrumb (8px), table wrap (10px).
- **Background:** Card White / Mist Header over Cool Paper.
- **Shadow Strategy:** None on guest (see Elevation).
- **Border:** 1px Border Cool on breadcrumb and table wrap.
- **Internal Padding:** Table wrap 0.5rem; page padding ~1.25–1.5rem; WinUI page grids Margin 16, spacing 12–16.

### Inputs / Fields
- **Style:** WinUI `TextBox` / theme defaults; chat and search use standard Fluent fields.
- **Focus:** System focus visual — do not replace with custom glow.
- **Error / Disabled:** Prefer Fluent validation patterns; guest uses Slot Red text for hard failures.

### Navigation
- **Desktop:** `NavigationView` + `TitleBar` with pane toggle; menu glyphs (Segoe MDL2); footer Settings item. Pattern library: PowerToys Settings UI.
- **Guest:** Breadcrumb path strip (Mist Header, 8px radius, System Blue links) — not a second app shell.

### File table (signature — guest)
- Sortable header labels (uppercase Label style, Muted Slate).
- Row hover: 8% System Blue mix.
- Icon + optional `fap://` download affordance (16px) beside names.
- Footer meta centered, Muted Slate, ~0.9rem.

### Lists / status (desktop)
- ListView sections (Peers, Chat, Transfers) with Header text; status line SemiBold 18px.
- Secondary lines Opacity ~0.75 at 12px — never drop below readable contrast on the active theme.

## 6. Do's and Don'ts

### Do:
- **Do** keep connection, peer, and transfer status visually louder than decorative chrome.
- **Do** pattern desktop settings and nav after PowerToys Settings (`microsoft/PowerToys` `src/settings-ui`).
- **Do** keep guest web thin: directory listing, breadcrumb, slot truth, download handoff.
- **Do** use Cool Paper / Mist Header / Card White tonal steps and 1px Border Cool for structure on guest web.
- **Do** meet WCAG 2.2 AA for guest text/contrast; keep WinUI AutomationIds and keyboard paths intact.
- **Do** respect `prefers-reduced-motion` on web and system motion settings on desktop.

### Don't:
- **Don't** recreate legacy dense P2P clients: cluttered toolbars, tiny fonts, every control visible at once.
- **Don't** turn guest web into a second full client or a consumer cloud-drive marketing page.
- **Don't** invent custom decorative shadows, glass stacks, or neon/RGB accents.
- **Don't** use a second display typeface or File Explorer–style chrome as the product metaphor.
- **Don't** spend System Blue / Slot Green / Slot Red on inactive decoration — they are interactive and semantic only.
- **Don't** pair 1px borders with soft wide drop shadows (ghost-card) on guest panels.
