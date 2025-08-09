# Search System Upgrade Plan (.NET 9)

## Overview
This plan upgrades the search system from .NET Framework 4.0 to .NET 9 without changing functional behavior. It focuses on modern hosting, serialization, logging, and async improvements, keeping the `SEARCH` verb and filters intact.

## Current State Analysis
- Verb: `SearchVerb` (pattern + optional date/size filters)
- Service: `ShareInfoService` builds and searches an on-disk cached tree per share
- Transport: Kestrel via ASP.NET Core (`ModernNodeServer` → `ListenerService` → handlers) for `/Fap.app/SEARCH`
- JSON: System.Text.Json (source-generated via `FapJsonContext`)
- Concurrency: Request path async; search internals mostly synchronous (OK)

## Target State (.NET 9)
- Hosting: Kestrel/ASP.NET Core route (already in place)
- JSON: System.Text.Json with source generation (already in place)
- Logging: Microsoft.Extensions.Logging; structured events for query and latency
- Concurrency: async request path; preserve synchronous search internals initially
- DI: Microsoft.Extensions.DependencyInjection (already in place)

## Migration Strategy

### Phase 1: Foundations
- Update TFM to net9.0 for Domain code containing `ShareInfoService` and `SearchVerb`
- Introduce logging abstractions in search paths
- Ensure `SearchVerb` serialization/deserialization uses System.Text.Json

### Phase 2: Transport Integration
- [x] Map `/Fap.app/SEARCH` in ASP.NET Core; decode via Multiplexor (centralized in server)
- [x] Forward to `SearchVerb.ProcessRequest`
- [x] Preserve request/response shape and semantics

### Phase 3: Async/Perf
- [x] Async HTTP handler in client/server handlers
- [ ] Add cancellation support and max-duration guard (optional)
- [ ] Consider degree-limited parallelization in `SearchRecursive` only if needed (future)

### Phase 4: Diagnostics
- [ ] Structured logs: SearchRequested(SearchString, Filters, Node), SearchCompleted(ResultCount, ElapsedMs)
- [x] Metrics counters: requested/completed/failures (health details)

## Testing Strategy
- Unit: `SearchVerb` JSON roundtrip; `StringMatcher` edge cases; filter logic
- Integration: end-to-end `/Fap.app/SEARCH` over Kestrel; compatibility with legacy clients
- Performance: baseline vs .NET 9 latency on large share indices

## Risk Assessment
- Low: JSON behavior differences; mitigate with tolerant readers and tests
- Low: Hosting changes; minimized by adapter preserving protocol

## Benefits
- Modern hosting and structured logging
- Faster, lower allocation JSON
- Async request handling and cancellation

## Migration Checklist
- [x] Net9 TFM on Domain/Application search-related projects
- [x] Kestrel route for `/Fap.app/SEARCH`
- [x] System.Text.Json for `SearchVerb` and `SearchResult`
- [x] Logging and basic metrics in search path (counters)
- [ ] Async HTTP handling with cancellation (optional)
- [ ] Unit/integration/performance tests

## Migration Timeline
- Week 1: TFM + JSON + logging
- Week 2: Kestrel mapping + async handler, tests
- Week 3: Metrics and perf validation
