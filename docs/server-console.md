## Server.Console (Overlord) – Running and CLI Overrides

Server.Console hosts the Overlord on Kestrel. It shares services with the main app and exposes HTTP and FAP endpoints.

### Run locally
```
dotnet run --project UI/Server.Console
```

### CLI overrides (configuration)
You can override settings using `--Section:Subsection:Key` syntax. Common overrides:

```
--Fap:Web:Listen:Address 127.0.0.1
--Fap:Web:Listen:Port 40
--Fap:Web:EnableCompression true
--Fap:Web:EnableCaching true
--Fap:Web:StaticFilesCacheSeconds 600
```

Example: bind to 127.0.0.1:4040
```
dotnet run --project UI/Server.Console -- --Fap:Web:Listen:Address 127.0.0.1 --Fap:Web:Listen:Port 4040
```

### Health and smoke endpoints
- `GET /Fap.api/health` → 200 OK
- `GET /Fap.api/compare/v1` → JSON
- `GET /Fap.app.web/template.html` → HTML

### Notes
- Default Overlord port is 40. Use CLI overrides to change.
- In tests, logging is reduced to keep output concise; production defaults are configurable via `appsettings.json`.


