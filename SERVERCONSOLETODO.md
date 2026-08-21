## Server.Console (.NET 9) – Upgrade TODO

The console Overlord server should remain a headless host that composes the same services as the main app and exposes the HTTP/FAP endpoints via Kestrel. Current status: targets `net9.0-windows`; uses `Host.CreateApplicationBuilder`, MEL logging, and `ApplicationCore.Load(...); StartOverlordServer()`.

### Must-do
- [x] Remove legacy `app.config`
  - Delete `UI/Server.Console/app.config` (Framework-only). Ensure no code reads it.
- [x] Verify DI composition matches main app
  - Extract/centralize composition into a shared extension to avoid drift (e.g., `services.AddFapServer()` in `FAP.Application`). Reuse here.
  - Confirm registrations in `Program.cs` map to current types:
    - `ShareInfoService`, `ListenerService`, `Model`, `ModernHTTPHandler`, `LANPeerFinderService`, `BufferService`, `ServerUploadLimiterService`, `OverlordManagerService`.
    - App controllers: `ConversationController`, `ConnectionController`, `WatchdogController`, `InterfaceController`, `ApplicationCore`.
    - Console views/services: `IMessageService` ⇢ `MessageService`, `IInterfaceSelectionView` ⇢ `InterfaceSelectionView`, `ISharesView` ⇢ `SharesView`, `IQuery` ⇢ `Query`.
- [x] Configuration binding
  - Ensure `appsettings.json` keys align with options used by `ModernNodeServer`:
    - `Fap:Web:EnableCompression`, `EnableCaching`, `EnableRateLimiting`, `StaticFilesCacheSeconds`.
    - `Fap:Web:Listen:Address`, `Port`.
    - `Fap:Web:KestrelLimits:*` (see current file for shape).
  - Add command-line overrides support (already available via `Host`): document usage, e.g. `--Fap:Web:Listen:Port 4040`.
- [x] Options validation (NET 9)
  - Use options validation on startup to fail fast on bad config:
    - `services.AddOptions<FapWebOptions>().Bind(configuration.GetSection("Fap:Web")).ValidateDataAnnotations().ValidateOnStart();`
    - `services.AddOptions<FapListenOptions>().Bind(configuration.GetSection("Fap:Web:Listen")).ValidateDataAnnotations().ValidateOnStart();`
- [x] Logging
  - Keep MEL providers (Console, Debug, optional EventLog). Remove commented/legacy NLog remnants.
  - Set minimum level from config (already present). Verify category levels are reasonable for production.
- [x] Startup/shutdown behavior
  - Use `IHostApplicationLifetime` to log start/stop and handle Ctrl+C gracefully. Ensure `ApplicationCore` stops services on shutdown.
- [x] Static content
  - Verify `Web.Resources` is copied to output alongside the server exe. If needed, ensure `FAP.Domain` marks assets as `Content` with `CopyToOutputDirectory=PreserveNewest`.
- [x] Health endpoints
  - Confirm `/Fap.api/health` and `/Fap.api/health/details` are reachable when launched. Document quick smoke tests (below).
- [ ] Add standard HealthChecks (optional)
  - `services.AddHealthChecks()` and map `/healthz` for infra/CI probes.
- [ ] Output caching (optional)
  - Consider `services.AddOutputCache()` + `app.UseOutputCache()` for static/idempotent GETs to improve perf on LAN.
- [x] Port and binding defaults
  - Default to `0.0.0.0:40` per legacy. Document that ports <1024 may require elevation; allow override via CLI.
- [x] Publishing
  - Validate single-file, self-contained publish (as per csproj):
    - `dotnet publish UI/Server.Console -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true`
  - Run the published binary and repeat smoke tests.

### Nice-to-have
- [ ] Cross-platform build option (opt-in)
  - Consider `net9.0` target and `win-x64` RID toggle. EventLog provider should be conditional on Windows.
- [ ] Structured logging
  - Add JSON console logging profile for log shipping (optional config profile).
- [ ] Service install script
  - Provide a PowerShell script to register as a Windows service using `sc.exe` or `PowerShell New-Service`.

### Automated testing (CI-friendly)
- [x] Create `FAP.Testing` project (class library)
  - Provide a reusable "scenario runner" that executes a configurable mix of FAP verbs against a target server using `FAP.Domain.Net.Client`.
  - Options: clients, parallelism, iterations, timeouts, pass/fail thresholds.
- [x] Add `tests/FAP.IntegrationTests` (xUnit)
  - Start Server.Console on a free port or compose `ModernNodeServer` directly via DI.
  - Wait for `/Fap.api/health` (and `/healthz` if enabled).
  - Run a tiny scenario (e.g., 5 clients × 2 iterations) via the scenario runner.
  - Assert success rate ≥ threshold; assert key endpoints respond.
- [ ] CI wiring
  - Update `build.ps1` to run `dotnet test` after build.
  - Optional extra job: run Server.Console, wait for health, run Client.Console with minimal workload, ensure zero exit code; then stop server.

### Smoke test checklist
1. Build: `dotnet build UI/Server.Console`.
2. Run: `dotnet run --project UI/Server.Console -- --Fap:Web:Listen:Address 127.0.0.1 --Fap:Web:Listen:Port 4040`.
3. Hit endpoints:
   - `GET http://127.0.0.1:4040/Fap.api/health` ⇒ `OK`.
   - `GET http://127.0.0.1:4040/Fap.api/health/details` ⇒ JSON with metrics.
   - `GET http://127.0.0.1:4040/Fap.app.web/template.html` ⇒ legacy HTML.

### Docs to update
- [ ] `docs/server-console.md`: reflect .NET 9, configuration keys, CLI overrides, and health endpoints.

### Future test expansion (not blocking)
- Unit tests for domain logic (e.g., `ShareInfoService`, `OverlordManagerService`, verb request/response handling).
- Contract tests for HTTP endpoints (typed compare API, health, static files) using `WebApplicationFactory`.
- Load/perf harness using the scenario runner with larger workloads (non-CI).
- Property-based tests for request encoding/decoding in `Multiplexor`.
- Security and static analysis gates (e.g., analyzers, dependency vulnerability scan) in CI.


