# FAP Migration TODO

This document tracks code changes to complete the .NET 9 core migration and quality improvements.

## High priority

- ~~WPF client startup should not start an overlord server~~ ✅ **DONE** — `App.xaml.cs` already calls `core.StartClient()`.

- ~~Remove duplicate service registrations in DI~~ ✅ **DONE** — `ModernHTTPHandler` duplicate removed; comment in `RegisterNetworkServices` confirms.
  - **Remaining**: `RegisterGUIServices` re-registers `ShareInfoService` and `DownloadQueueController` which are already in `RegisterDomainServices`/`RegisterApplicationServices`. Remove the duplicates in `RegisterGUIServices`.

- Adopt IHttpClientFactory
  - Add `services.AddHttpClient("FapDefault")` in DI startup with default timeout and UA header.
  - Refactor `FAP.Domain/Net/ModernHttpClient.cs` to accept an `HttpClient` (via factory) rather than `new HttpClient()`.
  - Refactor `FAP.Domain/Services/DownloadWorkerService.cs` to receive an `HttpClient` (via factory) and remove direct instantiation.
  - Replace scattered `new ModernHttpClient(...)` creations with either DI-resolved client or a factory wrapper that uses `IHttpClientFactory`.

- Replace Thread.Sleep/ThreadPool.QueueUserWorkItem with async equivalents
  - `FAP.Application/ApplicationCore.cs`:
    - `MainWindowUpdater` loop → async method using `PeriodicTimer` and `CancellationToken`.
    - `Exit`/shutdown path: avoid blocking calls.
  - `FAP.Application/Controllers/ConnectionController.cs`:
    - Replace `ThreadPool.QueueUserWorkItem` and manual waits with async loops, `Task.Run`, `PeriodicTimer`, and `await`.
    - Replace `.Result`/`.Wait()` usages on async with `await`.
  - `FAP.Application/Controllers/WatchdogController.cs`:
    - Convert background scan loop to async with `PeriodicTimer` and cooperative cancellation.
  - `DownloadWorkerService`: prefer `ReadAsync` and async flows throughout download processing.

- Registry protocol registration to HKCU instead of HKLM
  - Update `FAP.Foundation/RegistryServices/RegisterProtocolService.cs` to default to `Registry.CurrentUser`.
  - Document elevation not required for per-user registration.

- Async correctness nits
  - Replace `GetAwaiter().GetResult()` bridges with async flows (`ModernHTTPHandler.Handle` and others).
  - Eliminate `.Result` in networking controller paths.

## Medium priority

- Options validation
  - Add validation for `FapWebOptions` and `FapListenOptions` via `services.AddOptions<T>().Validate(...)`.

- File IO enhancements
  - Prefer `FileStreamOptions` and `CopyToAsync` for large file transfers.
  - Consider `MemoryPool<byte>` for buffer reuse.

- JSON source-gen
  - Where performance-sensitive, use `FapJsonContext.Default` overloads consistently.

## Low priority / defer (UI phase)

- Project references/TFMs cleanup
  - Remove `<UseWPF>` from non-UI projects and `Microsoft.AspNetCore.App` from `FAP.Domain` when UI overhaul starts.

## Documentation (after code changes)

- Update docs to match current code:
  - `docs/download-transfer-management.md`: replace `HttpWebRequest` and Newtonsoft examples with `HttpClient` and System.Text.Json.
  - `docs/configuration-settings.md`: remove leftover `JsonConvert` and `Formatting`; align with `BaseEntity` methods.
  - `docs/connection-management.md`: replace NLog `LogManager` calls with `ILogger` samples; async usage in examples.
  - Fix broken links in `docs/README.md` (remove or add `network-components.md`, `web-interface.md`).
  - Ensure config examples match actual `appsettings.json` structure.

## Cross-reference (files to edit)

- `UI/Client.WPF/App.xaml.cs`
- `FAP.Application/ApplicationCore.cs`
- `FAP.Application/Controllers/ConnectionController.cs`
- `FAP.Application/Controllers/WatchdogController.cs`
- `FAP.Domain/Net/ModernHttpClient.cs`
- `FAP.Domain/Services/DownloadWorkerService.cs`
- `FAP.Foundation/RegistryServices/RegisterProtocolService.cs`
- `FAP.Domain/Handlers/ModernHTTPHandler.cs` (async Handle)


