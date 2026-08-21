# FAP 10.0.0 Beta: .NET 10 Upgrade Plan

**Target**: `migration/net10-upgrade` branch → FAP 10.0.0 beta  
**Base**: FAP 9.0.0 (master, commit cf88640)

## Why .NET 10

.NET 10 is LTS (3 years support) vs .NET 9 STS (18 months). This makes FAP 10.0.0 the long-lived product line.

Runtime improvements in 10 directly benefit high-bandwidth transfers:
- Better escape analysis and inlining → less GC pressure on buffer paths
- AVX10.2 SIMD improvements for multicast/HTTP paths
- ASP.NET 10 memory-pool eviction tuning for Kestrel (GuestWeb)

## Upgrade Scope

### 1. Retarget TFMs
- Core projects: `net9.0` → `net10.0`
- WinUI client: `net9.0-windows10.0.19041.0` → `net10.0-windows10.0.19041.0`
  - WinUI 3 remains on Windows App SDK; the TFM is not a new WinUI generation

### 2. Runtime Validation
**Do not assume** JIT wins translate to real FAP workload gains. Measure:
- File-copy path: `DownloadManager` → disk throughput
- HTTP path: Kestrel ingress/egress on GuestWeb static file serving
- Multicast discovery: announcement/response latency

Capture baseline from 9.0.0 master before merge.

### 3. Library Updates (10-specific wins)
- **ASP.NET 10**: Enable memory-pool eviction tuning in `Server.Console` and GuestWeb Kestrel hosts
- **C# 14 / Span**: First-class `Span` conversions on buffer paths (`FAP.Network`, `FAP.Domain` transfer sessions)
  - Target: eliminate allocations in `SessionTransmitter`, `SessionReceiver`, multicast send/receive

### 4. Pick ONE Beta Goal
Do not start both:

**Option A**: Native AOT for `Server.Console`  
- Headless server; no WinUI/WPF → good AOT candidate
- Measure startup time and steady-state memory vs JIT baseline
- Blocked by: any remaining reflection (DI, JSON serialization, protobuf-net)

**Option B**: Kill Framework-Era Allocations  
- Close existing GitHub issues #3–#7 (memory, ValueTask, Channels, SIMD)
- Audit: `MemoryPool<byte>`, `IAsyncEnumerable`, `ValueTask` conversions
- Focus: hot paths (file read/write, HTTP handlers, multicast)

**Decision**: TBD in beta iteration. Do not attempt both for 10.0.0 beta.

### 5. NOT in Scope
- This is **not** a product rewrite
- C# 14 syntax niceties (`field` keyword, extension members) are cleanup, not beta goals
- No WinUI 4 / WPF modernization (see `docs/upgrade-plan-ui.md` for existing UI plans)

## Tag Contract
- Beta releases: `v10.0.0-beta`, `v10.0.0-beta.2`, etc. (hyphen tags already mark GitHub prereleases)
- Stable release: `v10.0.0` (only after merge back to `master`)

## Rollout
1. This branch ships beta tags
2. After validation, merge back to `master` for stable `v10.0.0`
3. FAP version tracks .NET TFM: 10.0.0 = .NET 10, 9.0.0 = .NET 9
