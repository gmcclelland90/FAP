## TESTINGTODO – Replacing Client.Console with Automated Integration Tests

We are deprecating `UI/Client.Console` and replacing it with automated tests and a reusable testing library. This section tracks what’s done and what remains.

### Deprecation plan for Client.Console
- [ ] Mark `UI/Client.Console` as deprecated in repo docs (`docs/client-console.md`).
- [ ] Ensure no projects reference `Client.Console`.
- [ ] Remove `UI/Client.Console` from `Fap.sln`.
- [ ] Delete `UI/Client.Console` after CI is green with the new tests.

### Implemented
- [x] New test library `FAP.Testing` created; provides primitives and a default scenario runner (can expand later).
- [x] Integration test project `tests/FAP.IntegrationTests` with fixtures:
  - [x] Overlord fixture hosts server in-process on 127.0.0.1:40.
  - [x] Client fixture hosts a client node with a temporary share, on a free port (8030).
  - [x] Global non-parallel test execution to avoid port conflicts.
  - [x] Quiet logging during tests; concise step markers printed.
- Tests implemented and passing:
  - [x] Overlord health: `GET /Fap.api/health`.
  - [x] Overlord compare: `GET /Fap.api/compare/v1`.
  - [x] Static file: `GET /Fap.app.web/template.html`.
  - [x] Overlord protocol: CONNECT → INFO → NOOP → CHAT (with session secret).
  - [x] Client node: SEARCH, BROWSE, direct GET download, Range GET.

### Still to implement (kept from original intent)
- [ ] Multicast scenarios (network discovery):
  - [ ] HelloVerb (HELLO) and WhoVerb (WHO) over UDP multicast. Note: not HTTP; requires a dedicated harness or specialized fixture.
- [ ] Multiplexor encode/decode smoke test (symmetry check across a small fixed set).
- [ ] Broader verb coverage (optional next): UPDATE, ADDDOWNLOAD, error paths (bad/missing auth).
- [ ] Concurrency scenarios (multi-client concurrent downloads/searches).

### CI/build integration
- [x] `build.ps1` updated to optionally run `dotnet test` after build.
- [ ] Add CI job to run integration tests on PRs; optionally a larger nightly scenario.
- [ ] Document run commands and environment variables in `docs/testing-platform.md`.

### Notes
- This testing replaces the need for `Client.Console`. It is more reliable, runs in CI, and is easy to extend.
