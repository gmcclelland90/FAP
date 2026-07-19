# FAP domain glossary

Terms used across architecture reviews and UI plans.

| Term | Meaning |
|---|---|
| **Node** | A FAP peer on the LAN (client or overlord). |
| **Overlord** | Coordinator node that peers connect through for discovery and chat fan-out. |
| **Share** | A local folder exposed for browse/download. |
| **Browse** | Listing a peer’s shares via FAP `BROWSE` (desktop) or guest HTTP directory listing. |
| **Guest Web Share** | No-client LAN surface: plain HTTP browse/download at `http://peer:port/` with Range/ETag and optional `fap://` handoff. |
| **Search** | Fan-out query across online peers for filenames. |
| **Compare** | Collect peer hardware/spec scores (verb + typed `/Fap.api/compare/v1`). |
| **Download queue** | Client-side queued transfers with workers/slots. |
| **fap://** | Protocol URL that queues a download in the installed desktop client. |

## UI tracks

1. **Desktop client** — WinUI 3 / Windows App SDK (`UI/Client.WinUI`), Fluent shell. WPF (`UI/Client.WPF`) is legacy until cutover completes.
2. **Guest web** — Thin HTML from compiled Razor (`FAP.GuestWeb` / `Views/Guest/Browse.cshtml`) via `IBrowsePageHtmlRenderer`; assets under `FAP.Domain/Web.Resources`.
