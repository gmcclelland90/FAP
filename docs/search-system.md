# Search System

## Overview
The search system lets a client query another peer for files and folders matching a pattern and optional filters. It is implemented via the `SEARCH` verb over the FAP protocol and executed against the remote peer's cached share index.

## Components
- Verb: `FAP.Domain/Verbs/SearchVerb`
- Entity: `FAP.Domain/Entities/SearchResult`
- Service: `FAP.Domain/Services/ShareInfoService` (index building and searching)
- Transport/Hosting: ASP.NET Core Kestrel via `/Fap.app/SEARCH`
- Handlers: `FAP.Domain/Handlers/FAPClientHandler.HandleSearch` (client role), `FAP.Domain/Handlers/FAPServerHandler.HandleSearch` (server/overlord role)

## Indexing
- On each peer, `ShareInfoService` maintains a cache of share metadata under `%LOCALAPPDATA%/FAP/ShareInfo/`
- Cache contains directory trees with names, sizes, last modified
- On startup or share change, `RefreshPath` scans the filesystem and persists a compact model

## Query Flow
1. Client constructs `SearchVerb` with pattern and filters
2. Client sends `SEARCH` to a peer via HTTP `/Fap.app/SEARCH` (decoded centrally in `ModernNodeServer`)
3. Remote handler deserializes `SearchVerb` and calls `ShareInfoService.Search`
4. Results are returned as JSON and deserialized into `SearchVerb.Results`

## Matching
- Pattern syntax: simple wildcard with `*` tokens (case-insensitive)
  - Expression is split on `*` and each segment must appear in order
  - Example: `*movie*1080p*` matches names containing `movie` then `1080p`
- Filters (optional):
  - ModifiedBefore/ModifiedAfter (DateTime)
  - SmallerThan/LargerThan (numeric; compared to size/dir size)
- Limits: `Model.MAX_SEARCH_RESULTS` caps the number of returned results

## Result Model
- `SearchResult` fields:
  - `FileName` (string), `IsFolder` (bool)
  - `Path` (virtual path within shares)
  - `Size` (bytes), `Modified` (DateTime)
  - `ClientID` (origin peer ID), `User` (ignored in JSON)

## Virtualization & Distinctness
- If multiple shares have the same root name, paths are virtually merged for browsing
- Distinct option merges duplicates by name when browsing; search returns individual hits subject to limit

## Performance Considerations
- Searching uses in-memory cached directory trees; avoids full filesystem scans for most queries
- Recursion short-circuits once the result limit is reached
- Case-insensitive `IndexOf` over segments for simple and fast matching
- Request path is asynchronous; fan-out to multiple peers is parallelized with bounded degree; per-peer timeouts apply

## Security
- Search operates only within declared shares of the remote peer
- Paths are virtual and do not expose physical filesystem structure

## References
- `FAP.Domain/Services/ShareInfoService.cs`
- `FAP.Domain/Verbs/SearchVerb.cs`
- `FAP.Domain/Entities/SearchResult.cs`
- `FAP.Domain/Handlers/FAPClientHandler.cs` (HandleSearch)
- `FAP.Domain/Handlers/FAPServerHandler.cs` (HandleSearch)
