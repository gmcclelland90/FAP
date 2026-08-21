---
name: fap-time-to-connected
description: >-
  Optimize FAP open → Connected latency using the TimeToConnected benchmark and
  phase JSON under artifacts/connect-timing/. Use when the user mentions slow
  connect, time-to-connected, Watchdog election, overlord startup delay,
  disconnected-to-connected, or asks to run/improve the connect timing agent loop.
---

# FAP time-to-connected

Do not guess where time is spent. Run the harness, read phase JSON, then patch the dominant phase.

Oracle is **`ConnectionState.Connected`** (protocol), not UI. Port **40 must be free** before tests.

## Loop

```
Task Progress:
- [ ] Run TimeToConnected (or sweep-discovery-grace.ps1)
- [ ] Read latest-net9-*.json — note totalMs and largest phase gap
- [ ] Always check multi_cold_start (OverlordCount must stay 1 across repeats) before cutting grace
- [ ] Patch only the hot path for that phase
- [ ] Re-run; keep going until budgets pass (or user stops)
- [ ] Ship winning DiscoveryGraceMs as production default
```

### Run

From repo root (PowerShell):

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
dotnet test tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TimeToConnected"
```

Or: `.\scripts\bench-connect-timing.ps1`

**Sweep discovery windows** (writes `artifacts/connect-timing/sweep-discovery-grace.csv`):

```powershell
.\scripts\sweep-discovery-grace.ps1
# or: .\scripts\sweep-discovery-grace.ps1 -GraceMsValues 1000,500,200,100,50,0
```

Tighten asserts while iterating:

```powershell
$env:FAP_TTC_BUDGET_MS_SOLO_ELECT = '3000'
$env:FAP_TTC_BUDGET_MS_JOIN_EXISTING = '1500'
$env:FAP_TTC_BUDGET_MS_DEDICATED = '2500'
$env:FAP_TTC_BUDGET_MS_MULTI_COLD_START = '5000'
$env:FAP_TTC_DISCOVERY_GRACE_MS = '200'   # under test
$env:FAP_TTC_MULTI_REPEATS = '5'
```

### Read

Artifacts (gitignored):

```
artifacts/connect-timing/latest-net9-solo_elect.json
artifacts/connect-timing/latest-net9-join_existing.json
artifacts/connect-timing/latest-net9-dedicated.json
artifacts/connect-timing/latest-net9-connect_rtt.json
artifacts/connect-timing/latest-net9-multi_cold_start.json
artifacts/connect-timing/sweep-discovery-grace.csv
```

Schema: `totalMs`, `budgetMs`, `phases` (ms from T0), `metrics` (`DiscoveryMs`, `ConnectRttMs`, `ReverseInfoMs`).  
`multi_cold_start` also has `clients[]` and metrics `ClientCount`, `ElectAttempts`, `OverlordCount`, `MinTotalMs`, `MaxTotalMs`, `SpreadMs`, `Repeats`.

Interpret phases in order: `LoadDone` → `ClientListen` → `WhoSent` → `ElectStart` → `OverlordBound` → `HelloRx` → `ConnectStart` → `ReverseInfo*` → `Connected`.

## First knobs (usual order)

1. **`ElectStart` dominates** — `FapElectionOptions.DiscoveryGraceMs` in Watchdog (`WatchdogController`); clock starts at `OnClientListening` / `StartClient`. Production default **0** (TTC-validated). Sweep via `FAP_TTC_DISCOVERY_GRACE_MS` if races appear. Prefer joining `LANPeerFinderService.Peers`.
2. **`join_existing` has `ElectStart`** — grace too short or discovery bug (must use Peers, not `Network.Nodes`).
3. **`multi_cold_start` `OverlordCount` ≠ 1** — election race; keep Watchdog alive on port race; only then add priority jitter.
4. **`ReverseInfoMs` large on solo** — skip reverse INFO for same-host client-elected overlord.
5. **`dedicated` slow** — `StartOverlordServerAsync` should health-wait on `:40`, not a fixed sleep.

Do not change unrelated UI/protocol behavior. Keep multicast client+overlord coexistence (`ReuseAddress` / Stop).

## Budgets

| Scenario | CI baseline (loose) | Agent target |
|---|---|---|
| `solo_elect` | 15000ms | &lt; 3000ms |
| `join_existing` | 8000ms | &lt; 1500ms |
| `dedicated` | 10000ms | &lt; 2500ms |
| `multi_cold_start` | 20000ms | &lt; 5000ms; `OverlordCount`=1 over repeats |

`JoinVsSoloRatio` (join/solo) should be ≪ 1; if ~1, fix discovery before cutting grace.  
Never cut grace without a green `multi_cold_start` (race oracle).  
Ship the **smallest** `DiscoveryGraceMs` that keeps all scenarios green.

## Framework compare (optional)

```powershell
$env:FAP_LEGACY_ROOT = '<clone of https://github.com/Kayomani/FAP.git>'
.\scripts\bench-connect-timing.ps1
.\scripts\compare-connect-timing.ps1
```

Legacy currently emits `latest-net40-dedicated.json` (Server.Console start → `Server started`). Needs MSBuild + .NET Framework targeting pack. Not PR CI.

## More detail

- `docs/testing-platform.md` — Time-to-connected section
- `TESTINGTODO.md` — remaining budget tighten / legacy solo
