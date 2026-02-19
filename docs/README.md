# FAP (File Acceleration Protocol) Documentation

Welcome to the comprehensive documentation for the File Acceleration Protocol (FAP) system. This documentation covers the network architecture, protocol specifications, and implementation details.

## Table of Contents

### Core Architecture
- [Network Architecture Overview](network-architecture.md) - High-level overview of the FAP network design
- [Overlord System](overlord-system.md) - Detailed explanation of the overlord server system
- [Protocol Specification](protocol-specification.md) - Complete FAP protocol documentation

### Implementation Details
- [Multicast Discovery](multicast-discovery.md) - Peer discovery and announcement mechanisms
- [Connection Management](connection-management.md) - Client-server connection handling
- [HTTP Integration](http-integration.md) - Kestrel hosting, routing, and typed API

### Core Systems
- [File System & Sharing System](file-system-sharing.md) - Share management, file system abstraction, and virtual directories
- [Download & Transfer Management](download-transfer-management.md) - Download queue, transfer sessions, and upload management
- [User Interface Architecture](user-interface-architecture.md) - MVVM pattern, WPF, and UI components
- [Configuration & Settings](configuration-settings.md) - Model entity, settings management, and persistence

### Components
- [Protocol Verbs](protocol-verbs.md) - Complete list and specification of FAP verbs
- [HTTP API](API.md) - Typed API endpoints under `/Fap.api`
- [Chat System](chat-system.md) - Real-time chat and messaging
- [Search System](search-system.md) - Network file search
- [Compare Feature](compare-feature.md) - Hardware comparison between peers
- [Server Console](server-console.md) - Headless server mode
- [Testing Platform](testing-platform.md) - Testing infrastructure

## Quick Start

FAP is a HTTP-based LAN file sharing tool designed for high-bandwidth environments (Gigabit+). Key features include:

- **Automatic Peer Discovery**: Uses multicast to find peers on the LAN
- **Overlord System**: Automatic server election and management
- **Dual Interface**: Both native client and web browser access
- **High Performance**: Optimized for large file transfers
- **No File Hashing**: Can share 15TB+ in under a minute

## Architecture Overview

```
Client A ──┐
           ├── Overlord Server 1 ─── Overlord Server 2
Client B ──┘                        │
                                    ├── Client C
                                    └── Client D
```

The system uses a hierarchical architecture where:
- **Clients** connect to overlord servers
- **Overlords** form a mesh network
- **Multicast** enables automatic discovery
- **HTTP** provides the transport layer

## Protocol Stack

```
Application Layer: FAP Protocol (Custom verbs)
Transport Layer:   ASP.NET Core (HTTP/1.1) via Kestrel
Network Layer:     IP (TCP/UDP)
Link Layer:        Ethernet
```

## Getting Started

1. Read the [Network Architecture Overview](network-architecture.md) for a high-level understanding
2. Review the [Overlord System](overlord-system.md) to understand server management
3. Examine the [Protocol Specification](protocol-specification.md) for implementation details

## Contributing

This documentation is maintained alongside the FAP codebase. When making changes to the system, please update the relevant documentation files.

## License

This documentation is part of the FAP project and is licensed under the GNU General Public License v3 (GPLv3). 