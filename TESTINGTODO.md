## TESTINGTODO – Automated regression + agent feedback



### Deprecation plan for Client.Console

- [ ] Mark `UI/Client.Console` as deprecated in repo docs (`docs/client-console.md`).

- [ ] Ensure no projects reference `Client.Console`.

- [ ] Remove `UI/Client.Console` from `Fap.sln`.

- [ ] Delete `UI/Client.Console` after CI is green with the new tests.



### Implemented

- [x] Test library `FAP.Testing` with `DefaultScenarioRunner` (wired from xUnit).

- [x] `tests/FAP.IntegrationTests` fixtures via shipping `AddFapCore`:

  - [x] Overlord on `127.0.0.1:40`

  - [x] Client on `8030` with a temp share `hello.txt`

  - [x] Non-parallel execution; quiet logging; step markers

- [x] Overlord: health, compare/v1, guest root, guest-ui, static assets, CONNECT→INFO→NOOP→CHAT

- [x] Client: SEARCH, BROWSE, GET, Range GET, ETag/If-None-Match, ADDDOWNLOAD auth

- [x] Shipping composition smoke (`AddFapCore` + `AddFapClient` + UI stubs)

- [x] `tests/FAP.UnitTests` (ViewModels + Multiplexor)

- [x] `tests/FAP.GuestWeb.UITests` (Playwright)

- [x] Guest/WinUI capture scripts under `scripts/`

- [x] WinUI AutomationIds + `tests/FAP.WinUI.UITests` (FlaUI, Category=UI)

- [x] CI: `.github/workflows/test.yml` (unit + integration + guest UI)

- [x] Docs: must-not-break matrix in `docs/testing-platform.md`

- [x] Time-to-connected harness (`TimeToConnected` collection + `IConnectTimingProbe` + JSON artifacts)

- [x] `FapElectionOptions.DiscoveryGraceMs` + Watchdog uses `LANPeerFinderService.Peers`

- [x] `scripts/sweep-discovery-grace.ps1` + multi_cold_start repeats

- [x] `scripts/bench-connect-timing.ps1` / `scripts/compare-connect-timing.ps1` (Framework A/B via `FAP_LEGACY_ROOT`)



### Still to implement

- [x] `multi_cold_start` TimeToConnected (N clients race; OverlordCount=1 oracle)

- [ ] Multicast HELLO/WHO harness beyond join_existing / multi_cold_start

- [ ] Broader verb coverage (UPDATE error paths)

- [ ] Concurrency / nightly load scenarios

- [ ] Nightly WinUI FlaUI CI job

- [ ] Finish Client.Console deprecation

- [ ] Tighten TimeToConnected CI budgets toward agent targets (solo &lt; 3s, join &lt; 1.5s, multi &lt; 5s, dedicated &lt; 2.5s) — agent targets already green with DiscoveryGraceMs=0

- [ ] Legacy solo_elect process timing (beyond Server.Console dedicated)

