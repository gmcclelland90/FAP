## FAP HTTP API (experimental)

Base path: `/Fap.api`

All endpoints are HTTP over LAN (no TLS). Responses are JSON unless noted.

- Health
  - GET `/Fap.api/health`
    - 200 text/plain `OK`
  - GET `/Fap.api/health/details`
    - 200 application/json
    - Body: `{ uptimeMs, activeRequests, time, rateLimit429: { interactive, downloads, default }, timingsMs: { interactive: { count,total,avg }, downloads: {...}, default: {...} }, chat: { received, forwarded, failures, clientReceived, conversationSent, conversationDelivered }, search: { requested, completed, failures } }`

- Compare
  - GET `/Fap.api/compare/v1`
    - 200 application/json
    - Body (CompareResponseV1):
      - `allowed` (bool)
      - `denyReason` (string|null)
      - `nickname` (string)
      - `location` (string)
      - `specs` (CompareSpecsV1|null)
        - `cpu` { model, cores, threads, bits, maxClockMhz }
        - `memory` { totalBytes }
        - `gpu` { model, totalMemoryBytes, count } | null
        - `display` { primaryWidth, primaryHeight, totalWidth, totalHeight }
        - `storage` { totalBytes, freeBytes, count }
        - `network` { maxLinkSpeedbps }
        - `audio` { deviceName } | null
        - `score` (long)
    - Notes: Uses WMI; values may be 0 if unavailable. Scoped queries and caching TTL may be added later via querystring.

Legacy routes: none. Health endpoints are only under `/Fap.api/*`.
- FAP protocol routes continue under `/Fap.app/*` (handled by FAPServerHandler)


