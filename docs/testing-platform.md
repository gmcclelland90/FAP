## Testing Platform

This project replaces the legacy `Client.Console` with automated integration tests and a small reusable testing library.

### What the tests cover
- Overlord server:
  - Health: `GET /Fap.api/health`
  - Compare API: `GET /Fap.api/compare/v1`
  - Static content: `GET /Fap.app.web/template.html`
  - Protocol flow: CONNECT → INFO → NOOP → CHAT
- Client node:
  - SEARCH for a seeded file (`hello.txt`)
  - BROWSE a seeded share (`/Tmp`)
  - Direct file download (full GET) and Range GET

All tests print concise step markers to the console (e.g., `[Overlord] HEALTH ok`, `[Client] SEARCH ok`).

### How tests run
- Tests host an Overlord server (127.0.0.1:40) in-process via a fixture
- Tests host a Client node on a free port (e.g., 8030) with a temporary share
- Test parallelization is disabled to avoid port conflicts
- Logging is quieted to Warning during tests

### Run locally
```
dotnet test tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj -c Debug
```

Optional: set the overlord base URL
```
$env:FAP_SERVER_URL = "http://127.0.0.1:40"
dotnet test tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj -c Debug
```

### Extend tests
- Add new xUnit tests under `tests/FAP.IntegrationTests`.
- Reuse existing fixtures (`OverlordHostFixture`, `ClientHostFixture`) or create new ones.
- Print concise steps with `System.Console.WriteLine("[Area] STEP ok")`.

### Future expansion
- UDP multicast scenarios (HELLO/WHO) in a dedicated harness/collection
- Additional verbs (UPDATE, ADDDOWNLOAD), negative/edge cases
- Concurrency and throughput scenarios
- Optional nightly load tests using the testing library


