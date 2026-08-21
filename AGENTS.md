# Agent notes

## Design Context

Strategic product context lives in [`PRODUCT.md`](PRODUCT.md). Visual system capture lives in [`DESIGN.md`](DESIGN.md) (sidecar [`.impeccable/design.json`](.impeccable/design.json)). Impeccable live config for guest browse is under [`.impeccable/live/config.json`](.impeccable/live/config.json).

**Surfaces**

- Primary: Windows desktop client (`UI/Client.WinUI`) — WinUI 3 / Fluent.
- Secondary: Guest web file browser (`FAP.GuestWeb`, assets in `FAP.Domain/Web.Resources`) — thin no-client LAN browse/download, not a second full client.

**Feel target (use heavily)**

- Microsoft PowerToys Settings UI — Fluent restraint, navigable sections, clear density; **not** File Explorer.
- Upstream source to study and pattern-match: [microsoft/PowerToys `src/settings-ui`](https://github.com/microsoft/PowerToys/tree/main/src/settings-ui) (especially `Settings.UI` XAML/controls). Prefer concrete patterns from that tree over inventing custom chrome.

**UI verification**

- Do not guess UI state. Use the project skill [`.cursor/skills/fap-agent-feedback/SKILL.md`](.cursor/skills/fap-agent-feedback/SKILL.md) (`fap-agent-feedback`) to capture feedback packs (screenshots, HTML, a11y, UIA, test logs) via `scripts/capture-guest-feedback.ps1` and `scripts/capture-winui-feedback.ps1` before and after visual changes.
