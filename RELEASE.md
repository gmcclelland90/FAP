## FAP Release Guide

This repo includes a PowerShell script to publish and package the Windows client (and optionally the console server).

### Requirements
- PowerShell 7+
- .NET 9 SDK

### Quick start (Client only)

```powershell
pwsh -File .\build.ps1 -Configuration Release -Runtime win-x64
```

Outputs:
- artifacts/publish/client/win-x64/Release/ (published files)
- artifacts/release/FAP-Client-win-x64-Release-YYYYMMDD-HHMM.zip (packaged zip)

### Include Server package

```powershell
pwsh -File .\build.ps1 -Configuration Release -Runtime win-x64 -IncludeServer
```

Additional output:
- artifacts/publish/server/win-x64/Release/
- artifacts/release/FAP-Server-win-x64-Release-YYYYMMDD-HHMM.zip

### Notes
- The client publish is self-contained, single-file, ReadyToRun, target win-x64.
- System tray icon is loaded via WPF pack URI, so it works in single-file publish.
- You can override Configuration/Runtime, e.g. `-Configuration Debug`.
- For CI, invoke the same script and upload the zip(s) from `artifacts/release`.


