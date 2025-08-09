# FAP Protocol Verbs

This page catalogs all FAP protocol verbs, their purpose, request/response shapes, and where they are implemented in the codebase. FAP verbs are transported over HTTP at paths of the form `/Fap.app/VERB` and are encoded/decoded by the `FAP.Network/Multiplexor`.

See also: Protocol transport details in `protocol-specification.md`.

## Transport
- **Base path**: `/Fap.app/`
- **Parameter encoding**: optional query `p` contains base64-encoded UTF-8 text
- **Body**: verbs that carry data use JSON in the HTTP body (POST)
- **Headers**: `FAP-AUTH`, `FAP-SOURCE`, `FAP-OVERLORD` as needed

## Core Verbs

### CONNECT
- **Purpose**: Establish a client ↔ overlord session
- **HTTP**: `POST /Fap.app/CONNECT`
- **Body**: JSON with address, client type, secret
- **Handled by**: `FAP.Domain/Handlers/FAPServerHandler.HandleConnect`
- **Client verb**: `FAP.Domain/Verbs/ConnectVerb`

### INFO
- **Purpose**: Exchange node information (identity, shares, status)
- **HTTP**: `GET /Fap.app/INFO`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleInfo`, `FAP.Domain/Handlers/FAPServerHandler.HandleClient`
- **Client verb**: `FAP.Domain/Verbs/InfoVerb`

### BROWSE
- **Purpose**: Browse a peer's shared virtual filesystem
- **HTTP**: `GET /Fap.app/BROWSE?p=base64(/path)`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleBrowse`
- **Client verb**: `FAP.Domain/Verbs/BrowseVerb`

### GET
- **Purpose**: Download a file from a peer
- **HTTP**: `GET /Fap.app/GET?p=base64(/path/to/file)`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleGet`

### UPDATE
- **Purpose**: Notify about network/node updates
- **HTTP**: `POST /Fap.app/UPDATE`
- **Body**: JSON payload
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleUpdate`, `FAP.Domain/Handlers/FAPServerHandler.HandleUpdate`
- **Client verb**: `FAP.Domain/Verbs/UpdateVerb`

### SEARCH
- **Purpose**: Query peers for files matching a term
- **HTTP**: `GET /Fap.app/SEARCH?p=base64(query)`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleSearch`, `FAP.Domain/Handlers/FAPServerHandler.HandleSearch`
- **Client verb**: `FAP.Domain/Verbs/SearchVerb`

### CHAT
- **Purpose**: Send broadcast chat messages
- **HTTP**: `POST /Fap.app/CHAT`
- **Body**: JSON with `Nickname`, `Message`, `SourceID`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleChat`, `FAP.Domain/Handlers/FAPServerHandler.HandleChat`
- **Client verb**: `FAP.Domain/Verbs/ChatVerb`

### CONVERSTATION (Conversation)
- **Purpose**: Conversation messaging (note spelling of verb string)
- **HTTP**: `POST /Fap.app/CONVERSTATION`
- **Body**: JSON with `Nickname`, `Message`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleConversation`
- **Client verb**: `FAP.Domain/Verbs/ConversationVerb`

### COMPARE
- **Purpose**: Retrieve peer system specifications for comparison
- **HTTP**: `GET /Fap.app/COMPARE`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleCompare`, `FAP.Domain/Handlers/FAPServerHandler.HandleCompare`
- **Client verb**: `FAP.Domain/Verbs/CompareVerb`
- **Notes**:
  - Response contains `{ Allowed: bool, Node: CompareNode }`
  - Data is cached per peer for 5 minutes
  - Can be disabled via `Model.DisableComparision`

### NOOP
- **Purpose**: Keep-alive ping
- **HTTP**: `GET /Fap.app/NOOP`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleNOOP`, `FAP.Domain/Handlers/FAPServerHandler.HandleNOOP`

### DISCONNECT
- **Purpose**: Gracefully disconnect
- **HTTP**: `GET /Fap.app/DISCONNECT`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleDisconnect`

### ADDDOWNLOAD
- **Purpose**: Queue a remote file to the local download manager
- **HTTP**: `POST /Fap.app/ADDDOWNLOAD`
- **Handled by**: `FAP.Domain/Handlers/FAPClientHandler.HandleAddDownload`
- **Related**: `FAP.Domain/Verbs/LocalDownload` (local helper)

## Multicast (UDP) Discovery Verbs
Used for LAN discovery, not transported over HTTP.

### HELLO
- **Purpose**: Announce presence
- **Implementation**: `FAP.Domain/Verbs/Multicast/HelloVerb`

### WHO
- **Purpose**: Request announcements from peers
- **Implementation**: `FAP.Domain/Verbs/Multicast/WhoVerb`

## Implementation Notes
- All verbs implement `FAP.Domain/Verbs/IVerb`
- Encoding/decoding and header extraction are handled by `FAP.Network/Multiplexor`
- HTTP server dispatches requests to FAP handlers based on `User-Agent` beginning with `FAP`


