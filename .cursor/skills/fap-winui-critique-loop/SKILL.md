---
name: fap-winui-critique-loop
description: >-
  Sequentially raise each WinUI desktop page to Nielsen critique score ≥30 via
  capture → Impeccable critique → apply Priority Issues → recapture → re-score.
  Use when the user mentions winui critique loop, desktop critique loop, raise
  desktop pages to 30, or continue from .impeccable/winui-critique-loop/state.json.
---

# FAP WinUI critique loop

Do not guess UI state. Capture a feedback pack, read PNGs + UIA, critique, then edit.

Two layers:

| Layer | Role |
|-------|------|
| **This skill** | Playbook: page queue, score gates, apply rules, `state.json` |
| **Dynamic `/loop`** | Heartbeat: wake with “continue from state”; do **one** next step; re-arm if unfinished |

Never restart from page 1 on a wake. Always: read state → next unit → write state → re-arm or stop.

## Defaults

- Pass bar: Nielsen **total_score ≥ 30** / 40
- Apply: **all Priority Issues** (P0/P1/P2) from the latest snapshot. Skip critique “Ask the User”, minor observations, and provocative questions unless a Priority Issue depends on them
- Cap: **max 5** inner iterations per page
- Plateau: if score is **unchanged or worse for 2 consecutive** post-fix critiques, mark page `stuck` (`plateau`) and advance
- Order: table below, sequential, never parallel

## Page queue

| # | id | Critique target | Capture label |
|---|-----|-----------------|---------------|
| 0 | shell | `UI/Client.WinUI/MainWindow.xaml` | `shell` |
| 1 | home | `UI/Client.WinUI/Pages/HomePage.xaml` | `home` |
| 2 | browse | `UI/Client.WinUI/Pages/BrowsePage.xaml` (+ `BrowserView` in FeatureViews when needed) | `browse` |
| 3 | chat | `UI/Client.WinUI/Pages/ChatPage.xaml` | `chat` |
| 4 | search | `UI/Client.WinUI/Views/FeatureViews.cs#SearchView` | `search` |
| 5 | queue | `UI/Client.WinUI/Views/FeatureViews.cs#DownloadQueueView` | `queue` |
| 6 | shares | `UI/Client.WinUI/Views/FeatureViews.cs#SharesView` | `shares` |
| 7 | compare | `UI/Client.WinUI/Views/FeatureViews.cs#CompareView` | `compare` |
| 8 | settings | `UI/Client.WinUI/Views/FeatureViews.cs#SettingsView` | `settings` |

FeatureViews targets must stay scoped to **one** class so trends do not mix pages.

## State

Path: `.impeccable/winui-critique-loop/state.json`  
Schema: `.impeccable/winui-critique-loop/state.schema.json`  
Example: `.impeccable/winui-critique-loop/state.example.json`

Page statuses: `pending` | `in_progress` | `passed` | `stuck`

On every unit of work, rewrite `state.json` (and keep `updatedAt` ISO-8601).

## One unit of work (per wake / per turn)

```
1. Read state.json (create from state.example.json if missing)
2. Find first page with status pending or in_progress
3. If none → print scoreboard, STOP (do not re-arm /loop)
4. Else for that page:
   a. Ensure fresh pack (run capture if lastPack missing or stale after edits)
   b. If no score yet this iteration: run WinUI critique → persist snapshot → record score
   c. If score >= 30 → status=passed, advance, done for this unit
   d. If iterations >= 5 OR plateau (2 non-improving post-fix scores) → status=stuck, advance
   e. Else apply all Priority Issues → build WinUI → recapture → critique → compare trend → update scores/iterations
5. Write state.json
6. If unfinished pages remain → re-arm dynamic /loop wake; else scoreboard and stop
```

Prefer **one** of (b) or (e) per wake when context is tight. Never parallelize pages.

## Capture

From repo root (PowerShell):

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
.\scripts\capture-winui-feedback.ps1
```

Pack: `artifacts/agent-feedback/winui/<timestamp>/`  
Evidence per page: `{label}.png`, `uia-{label}.json` (also legacy `uia.json` = last page).

Also follow [fap-agent-feedback](../fap-agent-feedback/SKILL.md). Port **40** must be free before overlord hosts; FlaUI may kill `Fap.Client.WinUI`.

After edits:

```powershell
dotnet build UI/Client.WinUI/Client.WinUI.csproj -c Debug -p:Platform=x64
.\scripts\capture-winui-feedback.ps1
```

## WinUI Impeccable critique (autonomous)

Assessment B HTML / live overlay does **not** apply. For each page:

1. Read `{label}.png` with the image Read tool; read `uia-{label}.json`
2. Assessment A: design / Nielsen heuristics vs XAML/C# + PRODUCT.md / DESIGN.md (PowerToys Settings feel)
3. Assessment B: skip `detect.mjs` on XAML; note `Assessment B: N/A (WinUI — pack PNGs + UIA)` in the report (not a silent degraded dual-agent skip — still run Assessment A via sub-agents when available)
4. Persist with critique-storage (skill base under `~/.agents/skills/impeccable` if not vendored):

```powershell
$env:IMPECCABLE_CRITIQUE_META = '{"target":"<page id>","total_score":<n>,"p0_count":<n>,"p1_count":<n>}'
node "$env:USERPROFILE\.agents\skills\impeccable\scripts\critique-storage.mjs" slug "<critique target path>"
# write body file, then:
node "$env:USERPROFILE\.agents\skills\impeccable\scripts\critique-storage.mjs" write <slug> <body-file>
node "$env:USERPROFILE\.agents\skills\impeccable\scripts\critique-storage.mjs" trend <slug> 5
```

5. **Skip “Ask the User”** — go straight to applying Priority Issues when score &lt; 30
6. Fixes: direct WinUI edits (XAML / FeatureViews / code-behind). No `$impeccable live`

Load impeccable `reference/critique.md` + `reference/product.md` for scoring rules. PRODUCT.md register is `product`.

## Apply rules

- Implement every Priority Issue from the latest snapshot for the current page
- Stay on-brand: status before chrome, Fluent / PowerToys restraint, thin density without clutter
- Do not drive-by refactor unrelated pages
- After apply: build must succeed before recapture

## Scoreboard (when queue complete)

Print a table:

| Page | Status | Best score | Iterations | Last pack |
|------|--------|------------|------------|-----------|
| … | passed/stuck | n/40 | k | path |

Then stop the dynamic loop (no further wake).

## Invoke

**Start (Agent mode):**

> Run the winui critique loop

Agent: init `state.json` from example if needed → first unit (usually capture + Shell critique) → arm dynamic `/loop`.

**Arm / continue heartbeat:**

```text
/loop continue the winui critique loop from .impeccable/winui-critique-loop/state.json
```

On each wake: read this skill + state → one unit → re-arm if unfinished.

**Resume in a new chat:**

> Continue the winui critique loop from state

**Stop:**

> Stop the winui critique loop

Kill any sleeper PID; do not re-arm; leave `state.json` as-is for later resume.

## Dynamic /loop contract

- Mode: **dynamic** (no fixed interval)
- Sentinel payload prompt must include: continue winui critique loop + state path
- After a finished unit, re-arm with a short delay (e.g. 30–60s) so the next wake starts promptly
- If a build/capture is still running in a terminal, prefer waking on that command’s completion (or a longer fallback heartbeat) instead of double-running
- When all pages are `passed` or `stuck`: print scoreboard, **do not** re-arm

## Related

- [fap-agent-feedback](../fap-agent-feedback/SKILL.md) — capture packs
- [PRODUCT.md](../../../PRODUCT.md) / [DESIGN.md](../../../DESIGN.md) — product + visual system
- Impeccable critique reference: `~/.agents/skills/impeccable/reference/critique.md`
