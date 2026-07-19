# Product

## Register

product

## Platform

web

Primary surface is the **Windows desktop client** (WinUI 3 / Fluent). Guest web is a small no-client LAN file browser for peers without the app installed — not a marketing site and not the main product shell.

## Users

Power users, LAN parties, and labs on a trusted network. They open FAP to work the full peer toolkit — discovery, browse, search, chat, compare, and queued transfers — not just a one-off file drop.

## Product Purpose

FAP is zero-setup LAN file sharing: peers find each other, share folders, and move files without cloud or accounts. Success is finding peers quickly, browsing or searching what’s shared, and getting downloads moving with minimal fuss.

## Positioning

Zero-setup LAN file sharing that still works from a browser when someone doesn’t have the client.

## Brand Personality

**Fast, plain, capable.** Voice stays direct and technical without theater. The feel target is Microsoft PowerToys Settings — Fluent structure, clear navigation, restrained chrome — not File Explorer and not a consumer “drive” product. Treat the upstream WinUI source as a primary pattern library: [microsoft/PowerToys `src/settings-ui`](https://github.com/microsoft/PowerToys/tree/main/src/settings-ui) (especially `Settings.UI`).

## Anti-references

Legacy dense P2P clients: cluttered toolbars, tiny fonts, every control visible at once.

## Design Principles

1. **Status before chrome** — Connection, peers, and transfer progress stay obvious; decoration never competes with “am I connected / can I get the file.”
2. **Familiar Fluent restraint** — Follow PowerToys Settings patterns: navigable sections, readable density, OS-native controls over custom inventiveness.
3. **Guest web stays thin** — The browser surface is a capable directory listing and download handoff, not a second full client.
4. **Dense when useful, never cluttered** — Power-user information is available without stacking every action into one overloaded view.
5. **Practice the protocol’s promise** — Screens reinforce that this works on the LAN with no account, no cloud, and a fallback path for guests.

## Accessibility & Inclusion

Aim for **WCAG 2.2 AA** on guest web. WinUI controls should remain automation-friendly (stable AutomationIds, keyboard paths). Respect `prefers-reduced-motion` on web; prefer system motion settings on desktop.
