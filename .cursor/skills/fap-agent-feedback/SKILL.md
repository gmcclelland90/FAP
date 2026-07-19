---
name: fap-agent-feedback
description: >-
  Gather FAP UI/debug feedback packs (screenshots, HTML, a11y, UIA tree, test logs)
  via capture scripts and tests. Use when debugging guest web or WinUI, verifying
  visual/layout changes, troubleshooting failed UI tests, or when the user asks for
  agent feedback, capture packs, screenshots, or “what it looks like.”
---

# FAP agent feedback

Do not guess UI state. Capture a feedback pack, read it (including images), then change code.

Parity oracle is **functional/protocol behavior**, not WPF pixel look. Vision reviews PNGs for layout/taste; HTML/UIA for structure and clickability. Vision is not the CI gate.

## When to capture

- Guest web or WinUI looks wrong / layout questions
- Failed Playwright or FlaUI assert
- New feature UI: before/after pairs in the same pack folder (or two timestamps)
- User asks to “see” the UI or attach feedback for an agent

## Quick capture

From repo root (PowerShell). Prefer these scripts over ad-hoc PrintScreen.

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'   # if Desktop runtime 9 missing
.\scripts\capture-guest-feedback.ps1
.\scripts\capture-winui-feedback.ps1
```

Optional:

```powershell
.\scripts\capture-guest-feedback.ps1 -BaseUrl 'http://127.0.0.1:8040/'
.\scripts\capture-winui-feedback.ps1 -ExePath 'path\to\Fap.Client.WinUI.exe'
```

Packs land under (gitignored):

```
artifacts/agent-feedback/
  guest/<timestamp>/
  winui/<timestamp>/
```

Each pack should include:

| File | Use |
|---|---|
| `meta.md` | SHA, URL/exe, steps, expected vs observed |
| `*.png` / `page.png` / `shell.png` | Visual review (Read tool on images) |
| `page.html` | Guest DOM structure |
| `a11y.json` | Guest structure hints |
| `uia.json` | WinUI AutomationId/Name dump |
| `test-log.txt` | Failing asserts / FlaUI or Playwright output |

## Agent loop

```
1. Reproduce (test or manual)
2. Run capture script for the surface
3. Read meta.md + test-log.txt + HTML/UIA
4. Read PNG(s) with the image Read tool
5. Fix with smallest change
6. Re-run focused tests
7. Re-capture; compare before/after
```

Rules:

- Prefer **failing assert + pack** over vague “UI looks wrong”
- Stop after ~4 failed capture/fix cycles; report what blocked progress
- Free port **40** before overlord/integration hosts (stop running FAP client)
- Guest Playwright fixture uses port **8040**; WinUI FlaUI may kill `Fap.Client.WinUI`

## Which surface

| Symptom | Capture | Tests |
|---|---|---|
| Guest browse/download HTML/CSS/JS | `capture-guest-feedback.ps1` | `tests/FAP.GuestWeb.UITests` |
| WinUI shell/nav/features | `capture-winui-feedback.ps1` | `tests/FAP.WinUI.UITests` (`Category=UI`) |
| Protocol/HTTP only | none required | `tests/FAP.IntegrationTests` / `FAP.UnitTests` |

```powershell
dotnet test tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj -c Debug
dotnet build UI/Client.WinUI/Client.WinUI.csproj -c Debug -p:Platform=x64
dotnet test tests/FAP.WinUI.UITests/FAP.WinUI.UITests.csproj -c Debug
```

First-time Playwright: `pwsh -File tests/FAP.GuestWeb.UITests/install-playwright.ps1`

## WinUI AutomationIds (for UIA / FlaUI)

Nav: `nav.home`, `nav.browse`, `nav.chat`, `nav.search`, `nav.queue`, `nav.shares`, `nav.compare`, `nav.settings`  
Home: `home.peers`, `home.chat`, `home.transfers`  
Features: `search.query`, `queue.list`, `shares.list`, `settings.nickname`, `browse.files`, `feature.host`  
Capture labels (PNG + `uia-{label}.json`): `shell`, `home`, `browse`, `chat`, `search`, `queue`, `shares`, `compare`, `settings`

## Ad-hoc guest (no script)

Cursor browser tools are fine for a live guest URL. Still write findings into a pack folder (`meta.md` + screenshot) when handing off or looping.

## More detail

- Pack contract and matrix: [docs/testing-platform.md](../../../docs/testing-platform.md)
- Tracker: [TESTINGTODO.md](../../../TESTINGTODO.md)
- Commands and pitfalls: [reference.md](reference.md)
