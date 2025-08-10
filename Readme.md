## FAP (File Acceleration Protocol)

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License: GPLv3](https://img.shields.io/badge/License-GPLv3-blue.svg)](Licences/FAP%20Licence.txt)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](#)
[![Server: Kestrel](https://img.shields.io/badge/Server-Kestrel-2E7D32)](docs/http-integration.md)
[![Language: C#](https://img.shields.io/badge/Language-C%23-239120)](#)

LAN file sharing, chat, search, and system compare — all over plain HTTP. 🌐💬🔎📁

Runtime: .NET 9 • Platform: Windows (WPF client) • Transport: HTTP (LAN)

FAP is an HTTP-based LAN file sharing tool. It includes peer discovery, coordinated servers, chat, one-to-one conversations, system specification comparison, network-wide search, and queued downloads. Files can be browsed and downloaded from the native client or a built-in web interface.

### Features
- **Automatic discovery**: UDP multicast for finding peers on the LAN
- **Overlord coordination**: Automatic election and message routing
- **Browse & download**: From the client or via the web interface
- **Search**: Query files across peers
- **Chat**: Global chat and one‑to‑one conversations 
- **Compare**: Collect and display system specs from peers 
- **Download queue**: Queued downloads with per-peer workers and session tracking
- **Upload/download controls**: Configurable limits and queue management
- **HTTP API**: Typed endpoints
- **Web serving**: Static files plus dynamic pages
- **Logging & metrics**: Structured logging
- **Windows client**: WPF client

### Architecture
- **Hosting**: ASP.NET Core Kestrel (HTTP only, LAN use)
- **Routing**:
  - `/Fap.app/VERB` – FAP protocol verbs (handled by server-side verb handlers)
  - `/Fap.app.web/...` – Web interface
  - `/Fap.api/...` – Typed API

Routes overview:

| Area | Base path | Examples |
|---|---|---|
| Protocol (verbs) | `/Fap.app/` | `/Fap.app/CONNECT`, `/Fap.app/SEARCH?p=...` |
| Web UI | `/Fap.app.web/` | `/Fap.app.web/`, `/Fap.app.web/css/fap.css` |
| HTTP API | `/Fap.api/` | `/Fap.api/health`, `/Fap.api/compare/v1` |
- **Request decoding**: Centralized in the server; forwarded to handlers via context
- **Serialization**: System.Text.Json (with source generation where applicable)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Logging**: Microsoft.Extensions.Logging
- **System info**: `System.Management` via `HardwareInfoService`
- **Caching & limits**: Response compression/caching and rate limiting via middleware

Projects of interest:
- `UI/Client.WPF` – Windows client (WPF)
- `FAP.Network` – Server hosting, HTTP wrappers, Kestrel integration
- `FAP.Domain` – Verbs, handlers, entities, services
- `FAP.Foundation` – Core utilities, WMI/system info, caching
- `FAP.Shared` – Shared DTOs, metrics, JSON source-gen context

### Getting started
Requirements:
- .NET 9 SDK
- PowerShell 7+

Build and package the client using the script:

```powershell
.\build.ps1 -Configuration Release -Runtime win-x64
```

Outputs:
- Client publish: `artifacts/publish/client/<RID>/<Config>/`
- Zips under: `artifacts/release/`

See `RELEASE.md` for details.

Quick links:
- Docs index: `docs/README.md`
- Protocol: `docs/protocol-specification.md` and `docs/protocol-verbs.md`
- Network architecture: `docs/network-architecture.md`
- HTTP server & API: `docs/http-integration.md`, `docs/API.md`

### Configuration
Configuration is provided via `appsettings.json`. Example:

```json
{
  "Fap": {
    "Listen": { "Address": "0.0.0.0", "Port": 30 },
    "Web": {
      "Compression": { "Enabled": true },
      "Caching": { "Enabled": true },
      "RateLimiting": { "Interactive": { "PermitLimit": 30 } }
    }
  }
}
```

Notes:
- Clients typically listen on port 30; overlords listen on port 40.
- HTTPS is not used; the system is intended for trusted LAN environments.

### HTTP API
- Base path: `/Fap.api`
- Endpoints:
  - `GET /Fap.api/health` – simple health check
  - `GET /Fap.api/health/details` – JSON with counters and basic timings
  - `GET /Fap.api/compare/v1` – system specification snapshot

Details and response shapes are documented in `docs/API.md`.

### Documentation
Full documentation lives in the `docs/` folder:
- Start with `docs/README.md`
- Architecture: `docs/network-architecture.md`, `docs/overlord-system.md`
- Protocol: `docs/protocol-specification.md`, `docs/protocol-verbs.md`
- HTTP: `docs/http-integration.md`, `docs/API.md`
- Core systems: file sharing, search, chat, compare, downloads

### License
See the `Licences/` folder for license information.