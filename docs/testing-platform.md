## Testing Platform

Automated regression and agent-feedback tooling for FAP. Parity goal is **functional/protocol behavior** (not WPF pixel look). WinUI Fluent shell is expected to differ visually from legacy WPF.

### Projects

| Project | Role |
|---|---|
| `tests/FAP.UnitTests` | Headless ViewModel + Multiplexor unit tests |
| `tests/FAP.IntegrationTests` | In-process overlord/client HTTP + protocol smokes |
| `tests/FAP.GuestWeb.UITests` | Playwright guest-web click/screenshot smokes |
| `tests/FAP.WinUI.UITests` | FlaUI desktop smokes (`Category=UI`, not default PR CI) |
| `FAP.Testing` | Scenario runner library used by integration tests |

### Must-not-break matrix

| Area | Behavior to preserve | Test layer |
|---|---|---|
| Overlord HTTP | `/Fap.api/health`, `/Fap.api/compare/v1` | Integration |
| Guest browse | Root HTML, `/Fap.app.web` assets, breadcrumb, `#files` table | Integration + Playwright |
| Guest download | Full GET, Range, ETag / If-None-Match | Integration |
| Protocol | CONNECT→INFO, NOOP, CHAT, SEARCH, BROWSE | Integration |
| Downloads | ADDDOWNLOAD auth gate (`LocalNode.Secret`) | Integration |
| Composition | `AddFapCore` / `AddFapClient` resolve with UI stubs | Integration |
| Desktop VMs | Search/Shares/Queue/MainWindow bindings | Unit |
| Desktop shell | Nav landmarks via AutomationId | FlaUI (local/nightly) |
| Discovery | HELLO/WHO multicast | Future harness |
| Time-to-connected | Solo elect / join existing / dedicated → `ConnectionState.Connected` | Integration (`TimeToConnected`) |

### How integration tests run

- Overlord fixture hosts on `127.0.0.1:40` via shipping `AddFapCore` DI
- Client fixture hosts on port `8030` with a temp share (`hello.txt`)
- Parallelization disabled to avoid port conflicts
- Logging quieted to Warning; concise `[Area] STEP ok` markers printed
- **Port 40 must be free** (stop a running FAP client/overlord before tests)

### Run locally

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'   # if Desktop runtime 9 is not installed
dotnet test tests/FAP.UnitTests/FAP.UnitTests.csproj -c Debug
dotnet test tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj -c Debug
dotnet test tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj -c Debug
```

Optional overlord base URL:

```powershell
$env:FAP_SERVER_URL = "http://127.0.0.1:40"
```

WinUI UI tests (after building the client):

```powershell
dotnet build UI/Client.WinUI/Client.WinUI.csproj -c Debug -p:Platform=x64
dotnet test tests/FAP.WinUI.UITests/FAP.WinUI.UITests.csproj -c Debug
```

Or via build script:

```powershell
.\build.ps1 -RunTests
.\build.ps1 -RunUiTests
```

### Time-to-connected (agent loop)

Measures real client path: `Load` → Watchdog election (if needed) → overlord bind → Hello → CONNECT → `Connected`.  
Writes JSON under `artifacts/connect-timing/` (gitignored).

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
# Port 40 must be free
dotnet test tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TimeToConnected"
# Or: .\scripts\bench-connect-timing.ps1
```

Artifacts (stable schema for agents/scripts):

```
artifacts/connect-timing/latest-net9-solo_elect.json
artifacts/connect-timing/latest-net9-join_existing.json
artifacts/connect-timing/latest-net9-dedicated.json
artifacts/connect-timing/latest-net9-connect_rtt.json
artifacts/connect-timing/latest-net9-multi_cold_start.json
```

Example JSON:

```json
{
  "version": 1,
  "scenario": "solo_elect",
  "runtime": "net9",
  "totalMs": 11240,
  "budgetMs": 15000,
  "phases": { "LoadDone": 80, "ElectStart": 10020, "OverlordBound": 10150, "HelloRx": 10180, "Connected": 11240 },
  "metrics": { "DiscoveryMs": 160, "ConnectRttMs": 1040, "ReverseInfoMs": 900 }
}
```

`multi_cold_start` adds `clients[]` (per-host phases / `elected`) and metrics `ClientCount`, `ElectAttempts`, `OverlordCount`, `MinTotalMs`, `MaxTotalMs`, `SpreadMs`, `Repeats`. Count via `FAP_TTC_MULTI_CLIENT_COUNT` (default 3); repeats via `FAP_TTC_MULTI_REPEATS` (default 5).

**Discovery knob:** `FapElectionOptions.DiscoveryGraceMs` (default **0**). Override in tests with `FAP_TTC_DISCOVERY_GRACE_MS`. Sweep: `.\scripts\sweep-discovery-grace.ps1`.

**Budgets** (CI baselines are loose; tighten via env while optimizing):

| Env | Default (approx) | Agent target |
|---|---|---|
| `FAP_TTC_BUDGET_MS` | applies to all if set | — |
| `FAP_TTC_BUDGET_MS_SOLO_ELECT` | 15000 | &lt; 3000 |
| `FAP_TTC_BUDGET_MS_JOIN_EXISTING` | 8000 | &lt; 1500 |
| `FAP_TTC_BUDGET_MS_DEDICATED` | 10000 | &lt; 2500 |
| `FAP_TTC_BUDGET_MS_MULTI_COLD_START` | 20000 | &lt; 5000; OverlordCount=1 |

**Agent loop:** sweep graceMs → read `latest-net9-*.json` (always include `multi_cold_start`) → patch hot path → ship smallest green `DiscoveryGraceMs`.  
First knobs: `DiscoveryGraceMs` (default 0), Watchdog Peers discovery, reverse INFO on same-host CONNECT, dedicated health wait.

**Framework A/B (local/nightly, not PR CI):**

```powershell
# Clone https://github.com/Kayomani/FAP.git somewhere, then:
$env:FAP_LEGACY_ROOT = "C:\path\to\Kayomani\FAP"
.\scripts\bench-connect-timing.ps1          # net9 tests + legacy Server.Console dedicated timing
.\scripts\compare-connect-timing.ps1        # fails if net9 worse than legacy by >10%
```

Legacy emits `latest-net40-dedicated.json` (totalMs from process start → `Server started`). Requires MSBuild + .NET Framework targeting pack.

### Agent feedback loop (UI)

Capture packs land under `artifacts/agent-feedback/` (gitignored). Attach the folder when asking an agent to debug UI.

```powershell
.\scripts\capture-guest-feedback.ps1
.\scripts\capture-winui-feedback.ps1
```

Layout:

```
artifacts/agent-feedback/
  guest|winui/<timestamp>/
    meta.md
    *.png
    page.html | uia.json
    test-log.txt   # optional
```

Rules for agents:

- Prefer **failing assert + artifact pack** over vague “UI looks wrong”
- For new features: capture before/after pairs
- Use vision on PNGs for layout/taste; use HTML/UIA for structure and clickability

### CI

GitHub Actions workflow `.github/workflows/test.yml` runs unit, integration, and guest Playwright tests on `windows-latest`. WinUI FlaUI tests stay opt-in (`-RunUiTests` / Trait `Category=UI`).

### Future expansion

- UDP multicast scenarios (HELLO/WHO) beyond join_existing / multi_cold_start
- Broader verb coverage (UPDATE error paths)
- Concurrency / nightly load via `DefaultScenarioRunner`
- Nightly WinUI FlaUI job
- Tighten TimeToConnected CI budgets toward agent targets; legacy solo_elect process harness
