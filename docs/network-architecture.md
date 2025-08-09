# Network Architecture Overview

## Introduction

The File Acceleration Protocol (FAP) implements a sophisticated peer-to-peer file sharing system designed for high-bandwidth LAN environments. The architecture combines automatic discovery, hierarchical server management, and HTTP-based communication to provide a robust and scalable file sharing solution.

## System Overview

FAP operates on a **hierarchical peer-to-peer architecture** with the following key components:

- **Clients**: End-user nodes that share and access files
- **Overlords**: Server nodes that coordinate network activity
- **Multicast Discovery**: Automatic peer detection mechanism
- **HTTP Transport**: Standard web protocol for communication

## Network Topology

```
                    ┌─────────────────┐
                    │   Multicast     │
                    │   Discovery     │
                    │  (239.1.1.1:12) │
                    └─────────────────┘
                            │
                            ▼
┌─────────────┐    ┌──────────────┐    ┌─────────────┐
│   Client A  │    │  Overlord 1  │    │   Client B  │
│             │◄──►│              │◄──►│             │
│ Web Server  │    │   Server     │    │ Web Server  │
│ HTTP:30     │    │ HTTP:40      │    │ HTTP:30     │
└─────────────┘    └──────────────┘    └─────────────┘
                            │
                            ▼
                    ┌──────────────┐
                    │  Overlord 2  │
                    │   Server     │
                    │ HTTP:40      │
                    └──────────────┘
                            │
                            ▼
                    ┌─────────────┐
                    │   Client C  │
                    │             │
                    │ Web Server  │
                    │ HTTP:30     │
                    └─────────────┘
```

## Key Architectural Principles

### 1. Automatic Discovery
- **Multicast Announcements**: Peers announce themselves via UDP multicast
- **Zero Configuration**: No manual server setup required
- **Dynamic Topology**: Network adapts to peer availability

### 2. Hierarchical Management
- **Overlord Election**: Automatic server selection based on capacity and priority
- **Load Distribution**: Clients connect to available overlords
- **Fault Tolerance**: Multiple overlords provide redundancy

### 3. Dual Interface
- **Native Client**: Rich desktop application with advanced features
- **Web Interface**: Browser-based access to shared files
- **Unified Server**: Single HTTP server handles both FAP protocol and web interface
- **URL-Based Routing**: Different URL paths distinguish protocol vs web requests

## Protocol Layers

### Application Layer (FAP Protocol)
```
FAP Verbs:
├── CONNECT    (Establish connection)
├── INFO       (Exchange node information)
├── BROWSE     (Browse shared files)
├── CHAT       (Send messages)
├── UPDATE     (Notify status changes)
├── SEARCH     (Search files)
├── COMPARE    (Compare system specs)
├── GET        (Download files)
└── NOOP       (Keep-alive)
```

### Transport Layer (ASP.NET Core HTTP)
- **Kestrel Hosting**: ASP.NET Core Kestrel serves protocol, web UI, and typed API
- **Custom Headers**: FAP-specific metadata via HTTP headers
- **POST/GET Methods**: RESTful communication patterns
- **JSON Payloads**: System.Text.Json
- **URL-Based Separation**: `/Fap.app/` for protocol, `/Fap.app.web/` for web interface; `/Fap.api/` for typed endpoints

### Network Layer (IP)
- **TCP**: Reliable HTTP communication
- **UDP**: Multicast discovery
- **IPv4**: Primary network protocol

## Communication Flow

### 1. Startup Sequence
```
1. Client starts (defaults to port 30)
2. Listen for multicast announcements
3. If overlord found → Connect as client to overlord (port 40)
4. If no overlord → Start local overlord (port 40)
5. Begin serving unified HTTP server (port 30)
   - FAP protocol requests: /Fap.app/VERB
   - Web interface requests: /Fap.app.web/
```

### 2. Message Routing
```
Client A → Overlord 1 → Overlord 2 → Client B
     │         │            │
     └─────────┴────────────┘
     (Direct multicast to all)
```

### 3. File Transfer
```
Client A → HTTP Request → Overlord → Client B
     │                              │
     └── Direct HTTP Download ◄─────┘
```

## Network Components

### Discovery Layer
- **MulticastClientService**: Listens for peer announcements
- **MulticastServerService**: Sends peer announcements
- **LANPeerFinderService**: Manages discovered peers

### Connection Layer
- **ConnectionController**: Handles client connections
- **FAPServerHandler**: Processes server requests
- **FAPClientHandler**: Processes client requests

### Transport Layer
- **ModernNodeServer**: ASP.NET Core Kestrel hosting and routing
- **ModernHttpClient**: Verb execution via HttpClient
- **Multiplexor**: Protocol encoding/decoding
- **Request Routing**: URL-based routing to FAP vs web handlers

## Scalability Features

### Automatic Load Balancing
- **Overlord Capacity**: Each overlord supports 40-50 clients (depending on priority)
- **Priority-Based Election**: Higher priority nodes become overlords first
- **Dynamic Scaling**: New overlords start when capacity is reached
- **Port Assignment**: Clients use port 30, overlords use port 40

### Fault Tolerance
- **Multiple Overlords**: Redundant server nodes
- **Automatic Reconnection**: Clients reconnect to available overlords
- **Graceful Degradation**: System continues with reduced capacity

### Performance Optimization
- **No File Hashing**: Eliminates startup delays
- **Direct Downloads**: Bypass overlord for file transfers
- **Connection Pooling**: Reuse HTTP connections
- **Chunked Transfers**: Support for large files

## Security Considerations

### Authentication
- **Secret Keys**: Unique authentication tokens per connection
- **Node Validation**: Verify peer identity before communication
- **Connection Limits**: Prevent resource exhaustion

### Network Isolation
- **LAN-Only**: Designed for trusted local networks
- **No Internet**: No external communication required
- **Firewall Friendly**: Uses standard HTTP ports

## Configuration

### Network Settings
- **Multicast Address**: 239.1.1.1
- **Multicast Port**: 12
- **Client Port**: 30 (unified HTTP server for FAP protocol + web interface)
- **Overlord Port**: 40 (unified HTTP server for FAP protocol + web interface)
- **Protocol Version**: FAP/1.0
- **URL Paths**: `/Fap.app/` (protocol), `/Fap.app.web/` (web interface)

### Performance Tuning
- **Kestrel Limits**: configured via appsettings (MaxConcurrentConnections, KeepAliveTimeout)
- **Compression/Caching**: enabled via middleware
- **Timeout Values**: per-call timeouts in client fan-out

## Monitoring and Debugging

### Logging
- **Microsoft.Extensions.Logging**: structured logs
- **Health Details**: `/Fap.api/health/details` JSON exposes counters and timings

### Diagnostics
- **Network State**: Connection status monitoring
- **Peer Discovery**: Multicast announcement tracking
- **Transfer Progress**: Real-time file transfer status

## Future Considerations

### Potential Enhancements
- **IPv6 Support**: Modern network protocol adoption
- **Encryption**: End-to-end data protection
- **WAN Support**: Internet-based file sharing
- **Mobile Clients**: Smartphone and tablet support

### Scalability Improvements
- **Distributed Overlords**: Geographic distribution
- **Load Balancing**: Advanced traffic distribution
- **Caching**: Intelligent data caching
- **Compression**: Bandwidth optimization 