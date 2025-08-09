# Chat System Upgrade Plan (.NET 9)

## Overview
This plan upgrades the chat system (global chat and one-to-one conversations) from .NET Framework 4.0 to .NET 9 without changing core functionality. Scope includes runtime, hosting, serialization, logging, and async improvements that come with .NET 9.

Out of scope: new features (presence, typing, persistence) and protocol changes. Those are covered in separate enhancement docs.

## Current State Analysis
- Verbs: `CHAT` (`ChatVerb`), `CONVERSTATION` (`ConversationVerb`)
- Handlers: `FAPClientHandler.HandleChat`, `FAPServerHandler.HandleChat`
- UI: `ConversationController`, `ConversationViewModel`, `Conversation` entity
- Transport: Kestrel via ASP.NET Core; routed through `ModernNodeServer` → `ListenerService` → FAP handlers
- JSON: System.Text.Json (source-generated via `FapJsonContext`)
- DI/Logging: Microsoft.Extensions.DependencyInjection / Microsoft.Extensions.Logging
- Concurrency: Async pipeline on server; client dispatch synchronous per message

## Target State (.NET 9)
- Hosting/Transport: Kestrel + ASP.NET Core pipeline (already in place)
- JSON: System.Text.Json (already in place)
- Logging: Microsoft.Extensions.Logging; structured logs for chat events
- Concurrency: async/await across message paths; bounded processing in overlord (optional)
- DI: Microsoft.Extensions.DependencyInjection (already in place)
- Compatibility: No protocol changes; verbs and payloads remain compatible

## Migration Strategy

### Phase 1: Foundations
- Add .NET 9 TFM to projects containing chat components
- Integrate Microsoft.Extensions.Logging in Domain and Application layers
- Introduce async method shapes but keep behavior the same

### Phase 2: Transport Integration
- [x] Route `/Fap.app/CHAT` and `/Fap.app/CONVERSTATION` via ASP.NET Core pipeline (mapping to existing handlers)
- [x] Use `Multiplexor` adapter for Kestrel; central decode in `ModernNodeServer`
- [x] Ensure overlord forwarding logic remains identical

### Phase 3: Serialization
- [x] System.Text.Json for chat verbs
- [x] Preserve field names and semantics; tolerant readers via `FapJsonContext`
- [x] Source-generated context entries for `ChatVerb` and `ConversationVerb`

### Phase 4: Logging and Diagnostics
- [x] Emit structured events/counters: ChatReceived, ChatForwarded, ConversationSent, ConversationDelivered
- [ ] Add failure counters and expose via health (partial: counters included in health details)

### Phase 5: Async/Perf
- [x] Make overlord chat forwarding async; avoid blocking on I/O
- [x] Add bounded concurrency on forwarding (semaphore-limited fan-out)
- [x] Keep 1:1 conversation send path async (cancellation token optional)

## Testing Strategy
- Unit tests: verb serialization/deserialization (roundtrip), handler logic
- Integration tests: end-to-end CHAT broadcast and CONVERSTATION delivery over Kestrel
- Compatibility: interop tests with legacy clients (if maintained in test harness)
- Performance: baseline vs .NET 9 throughput/latency for chat flows

## Risk Assessment
- Low: Serialization differences (Json.NET → System.Text.Json) causing edge-case behavior
- Low: Hosting migration risks mitigated by middleware adapter and unchanged protocol
- Medium: Async changes introducing race conditions; mitigated by tests and bounded concurrency

## Benefits
- Modern hosting and logging
- Reduced allocations and improved throughput with System.Text.Json
- Simplified async code and better responsiveness
- First-class diagnostics and metrics

## Migration Checklist
- [x] Add .NET 9 TFM to Domain/Application projects with chat
- [x] Wire `/Fap.app/CHAT` and `/Fap.app/CONVERSTATION` through Kestrel
- [x] Replace Json.NET with System.Text.Json for chat verbs
- [x] Add Microsoft.Extensions.Logging to chat paths
- [ ] Make overlord forwarding async and bounded (optional)
- [ ] Add unit/integration/perf tests for chat flows (optional)

## Migration Timeline
- Week 1: TFM update, logging integration, async method scaffolding
- Week 2: Kestrel routing + Multiplexor adapter, serialization swap
- Week 3: Async forwarding + bounded concurrency, tests and perf baselines
