# Chat System

## Overview
FAP provides two chat capabilities:
- Global chat via the `CHAT` verb (broadcasted through overlords to all connected clients)
- One-to-one conversations via the `CONVERSTATION` verb (directed to a specific peer)

This document explains message flow, data structures, handlers, and UI behavior for both chat modes.

## Architecture
- **Transport**: HTTP-based FAP protocol (`/Fap.app/CHAT`, `/Fap.app/CONVERSTATION`)
- **Encoding/Decoding**: `FAP.Network/Multiplexor`
- **Verbs**:
  - Global: `FAP.Domain/Verbs/ChatVerb`
  - Direct: `FAP.Domain/Verbs/ConversationVerb`
- **Handlers**:
  - Client: `FAP.Domain/Handlers/FAPClientHandler.HandleChat`
  - Server/Overlord: `FAP.Domain/Handlers/FAPServerHandler.HandleChat`
  - Conversation UI/logic: `FAP.Application/Controllers/ConversationController`

## Global Chat (CHAT)

### Verb
- Class: `ChatVerb`
- Fields: `Nickname`, `Message`, `SourceID`
- CreateRequest: serializes itself to JSON and sets `Verb = "CHAT"`
- ProcessRequest: echoes a response payload (used for forwarding)
- ReceiveResponse: deserializes incoming JSON into properties

### Routing
- Client receives HTTP request with `Verb = CHAT`:
  - `FAPClientHandler.HandleChat`:
    - Deserializes `ChatVerb`
    - Appends message to `model.Messages` rotating list
    - Sends HTTP 200 OK
- Overlord receives `CHAT`:
  - If `OverlordID` missing: set to local overlord, forward to standard clients and overlord clients
  - If `OverlordID` present: forward to standard clients only
  - Send empty response to sender

### UI Behavior
- Global chat messages appear in the client's messages area (e.g., status/log panel)
- Stored in `Model.Messages` with rotation limit

## Direct Conversations (CONVERSTATION)

### Verb
- Class: `ConversationVerb` (verb literal is "CONVERSTATION")
- Fields: `Nickname`, `Message`; `SourceID` is set from transport headers
- CreateRequest: sets `Verb = "CONVERSTATION"`, serializes payload
- ProcessRequest: deserializes payload and stamps `SourceID`

### Controller & Model
- Controller: `ConversationController` implements `IConversationController`
  - Maintains a collection of `Conversation` objects keyed by the other peer
  - Opens a popup/tab on first message with that peer
  - Sends messages asynchronously via `Client.Execute` with `ConversationVerb`
- Entity: `FAP.Domain/Entities/Conversation`
  - Holds `OtherParty` and message list (`SafeObservedCollection<string>`, `UIMessages`)
- ViewModel: `ConversationViewModel`
  - Binds `UIMessages`, highlights/flashes when new messages arrive and tab not active

### Message Flow
1. User types a message in a conversation popup and clicks Send
2. `ConversationController.SendChatMessage` enqueues `SendMessageAsync`
3. `SendMessageAsync` builds `ConversationVerb` with local nickname/ID, clears input, and executes the verb to the target `Node`
4. Receiver processes `CONVERSTATION`, controller finds/creates a `Conversation` for the sender, and appends the message

## Data & Serialization
- JSON: Newtonsoft.Json currently in verbs; future plan is System.Text.Json per upgrade plan
- Headers: `FAP-SOURCE` maps to `SourceID` in conversation handling
- Message format: minimal fields (nickname, message, source)

## Error Handling
- Conversation send failure: user sees a local system message indicating failure
- Overlord routing: best-effort forwarding; sender receives 200 OK after enqueue

## Security & Privacy
- Authentication: piggybacks on FAP headers/secrets
- Spoofing: messages rely on `SourceID` and nickname; consider future signatures if needed
- Content safety: no built-in filtering; client UI should sanitize/escape when rendering

## Extensibility
- Typing indicators, presence, read receipts can be added by new verbs/events
- Conversation history persistence can be layered on the `Conversation` entity
- Web UI can implement a chat panel using the same verbs via HTTP endpoints

## References
- `FAP.Domain/Verbs/ChatVerb.cs`
- `FAP.Domain/Verbs/ConversationVerb.cs`
- `FAP.Domain/Handlers/FAPClientHandler.cs` (HandleChat)
- `FAP.Domain/Handlers/FAPServerHandler.cs` (HandleChat)
- `FAP.Application/Controllers/ConversationController.cs`
- `FAP.Application/ViewModel/ConversationViewModel.cs`
- `FAP.Domain/Entities/Conversation.cs`
