# Compare Feature Upgrade Plan (.NET 9)

## Overview
This plan modernizes the Compare feature and the `COMPARE` verb for .NET 9.
Goals:
- Improve performance and responsiveness
- Strengthen privacy and configurability
- Make the data model strongly typed and versioned
- Align transport, JSON, and hosting with other .NET 9 upgrades

Related plans: HTTP Server → Kestrel, JSON → System.Text.Json, WMI upgrade.

## Current State Analysis

### Feature Summary
- Client enumerates peers and issues `GET /Fap.app/COMPARE` requests in parallel
- Each peer returns `{ Allowed: bool, Node: CompareNode }`
- `CompareNode` exposes typed properties backed by a stringly typed data store
- `CompareVerb` gathers specs via `SystemInfo` and caches results statically for 5 minutes
- Users may toggle `Model.DisableComparision` to deny comparisons

### Key Code
- `FAP.Domain/Verbs/CompareVerb`
- `FAP.Domain/Entities/CompareNode`
- `FAP.Application/Controllers/CompareController`
- Handlers: `FAP.Domain/Handlers/FAPClientHandler.HandleCompare`, `FAP.Domain/Handlers/FAPServerHandler.HandleCompare`

### Limitations
- Static cache in `CompareVerb` (process-wide, coarse, not easily testable)
- Blocking system info collection; uses synchronous APIs
- Stringly typed backing store in `Node` and implicit property keys
- No field-level consent or scoping (all-or-nothing via `DisableComparision`)
- No schema versioning for payloads
- No structured timeouts/limits; coarse 5-minute cache only
- Tightly coupled to custom HTTP stack and Newtonsoft.Json

## Target State (.NET 9)

### Functional Enhancements
- Field-level consent with presets (Minimal, Standard, Extended)
- Schema versioning for response payloads; tolerant readers for backward compatibility
- Configurable cache policy (absolute/sliding/TTL) per field group
- Better error detail codes (e.g., DENIED_POLICY, TIMEOUT, PARTIAL)

### Architectural Changes
- Hosting: Kestrel/ASP.NET Core Minimal API (align with HTTP server upgrade)
- Transport: keep `/Fap.app/COMPARE`; route via middleware/minimal endpoint
- JSON: System.Text.Json with source generators; camelCase properties
- Concurrency: async/await, `Parallel.ForEachAsync`, `Task.WhenAll`, cancellation tokens
- Caching: `IMemoryCache` (per-node, per-scope) instead of static fields
- System Info: move to `System.Management` providers on Windows; pluggable providers for cross-platform
- Telemetry & Logging: `Microsoft.Extensions.Logging`, EventCounters, Activity tracing

### Data Model
Strongly typed DTOs independent from the `Node` backing store.

```csharp
// DTO for response, versioned
public sealed record CompareResponseV1(
    bool Allowed,
    string? DenyReason,
    string Nickname,
    string Location,
    CompareSpecsV1? Specs
);

public sealed record CompareSpecsV1(
    CpuInfoV1 Cpu,
    MemoryInfoV1 Memory,
    GpuInfoV1? Gpu,
    DisplayInfoV1 Display,
    StorageInfoV1 Storage,
    NetworkInfoV1 Network,
    AudioInfoV1? Audio,
    long Score
);
```

### Consent Profiles
- Minimal: CPU model, cores/threads, RAM size, Score
- Standard (default): Minimal + GPU model/mem, storage totals, display totals
- Extended: Standard + motherboard/BIOS, per-display info, NIC speed, audio

## Migration Strategy

### Phase 1: Foundations (Week 1)
- Introduce `ICompareService` abstraction for system info with async APIs
- Add `IMemoryCache` for per-node compare results with TTL
- Introduce DTOs and response versioning (`v1`), mapped from existing entities
- Wire System.Text.Json and source generators for DTOs

### Phase 2: Server Endpoint (Week 2)
- Expose `GET /Fap.app/COMPARE` via ASP.NET Core middleware/minimal API
- Parse `scope` (minimal|standard|extended) and `maxAge` query params
- Implement cancellation and tiered timeouts per provider
- Return typed `CompareResponseV1` with error codes on failure/denial

```csharp
app.MapGet("/Fap.app/COMPARE", async (
    HttpContext ctx,
    ICompareService svc,
    IMemoryCache cache,
    ILogger<CompareEndpoint> log,
    CancellationToken ct) =>
{
    var scope = ctx.Request.Query["scope"].ToString();
    var key = CacheKey.From(ctx.Request.Headers, scope);
    if (cache.TryGetValue(key, out CompareResponseV1 cached))
        return Results.Json(cached);

    var result = await svc.GetSpecsAsync(scope, ct);
    cache.Set(key, result, TimeSpan.FromMinutes(5));
    return Results.Json(result);
});
```

### Phase 3: Client & UI (Week 3)
- Update client to request desired scope, pass cancellation tokens
- Use `Task.WhenAll` to fan-out to peers; limit concurrency with `SemaphoreSlim` or Channels
- Update UI to display partial results and error badges (Denied/Timeout/Partial)
- Add user setting for default scope and per-run override

### Phase 4: Hardening (Week 4)
- Add policy-based retries/backoff (Polly) for transient failures
- Add metrics (duration, cache hit rate, failure counts)
- Add integration tests with synthetic slow providers
- Document privacy policy and data handling

## Implementation Details

### CompareService Providers
- Windows: `System.Management` WMI queries; minimize property sets; parallelize independent queries
- Cross-Platform: Optional providers using `/proc`, `sysctl`, `dmidecode` (Linux/macOS) behind feature flags
- Pluggable via DI; choose provider based on OS

### Caching
- Use `IMemoryCache` with absolute TTL (default 5m) and per-scope overrides
- Cache key includes node identity + scope + schema version
- Avoid static global cache to ease testing and isolation

### Concurrency
- Use `Parallel.ForEachAsync` or Channels to bound concurrent peer requests (e.g., degree 16)
- Cancellation: per-peer timeout (e.g., 3s minimal, 7s standard, 12s extended) and global run timeout

### Model Refactor (Optional)
- Keep `CompareNode` for UI binding short-term; map from DTOs
- Long-term: migrate UI to bind directly to DTOs; remove stringly key store
- Maintain backward compatibility for on-the-wire parsing during transition

### Security & Privacy
- Field-level consent; deny extended scopes by default
- Optional HMAC signature for responses to prevent spoofing (future)
- Sanitize and cap string lengths; avoid leaking PII (e.g., BIOS serials)

## Risks
- Provider variance and latency (WMI can be slow on some systems)
- Backward compatibility with legacy clients (ensure tolerant readers)
- UI churn if switching bindings to DTOs

## Benefits
- Faster, more reliable comparisons with bounded latency
- Fine-grained privacy control and better UX feedback
- Strongly typed, versioned schema ready for future extension
- Testable, DI-friendly components and caching

## Testing Strategy
- Unit: DTO mapping, caching logic, consent/scope resolution, timeouts
- Integration: End-to-end COMPARE over Kestrel, mixed peers, denial paths
- Performance: Measure cold/warm timings per scope; cache hit rates
- Compatibility: Interop with legacy .NET Framework clients if needed

## Migration Checklist
- [ ] Create `ICompareService` and Windows provider
- [ ] Add DTOs (`CompareResponseV1`, `CompareSpecsV1`, sub-records)
- [ ] Add System.Text.Json source generation for DTOs
- [ ] Introduce `IMemoryCache` for compare responses
- [ ] Implement `/Fap.app/COMPARE` minimal API/middleware endpoint
- [ ] Update client to request scopes and handle partial/denied results
- [ ] UI: add scope selector, error badges, and live progress
- [ ] Metrics and logging via Microsoft.Extensions.Logging
- [ ] Documentation updates (`compare-feature.md`)

## Timeline
- Week 1: Services, DTOs, cache, JSON
- Week 2: Endpoint, DI, server wiring
- Week 3: Client fan-out, UI updates
- Week 4: Hardening, metrics, tests, docs
