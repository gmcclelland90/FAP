## FAP Release Guide

This repo includes a PowerShell script to publish and package the Windows client (and optionally the console server).

### Requirements
- PowerShell 7+
- .NET 9 SDK

### Creating an Official Release

To create and publish a release with GitHub artifacts:

1. **Tag the release version:**
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```

2. **Automated build:** The GitHub Actions workflow (`.github/workflows/release.yml`) will automatically:
   - Build both client and server artifacts using `build.ps1`
   - Create a GitHub Release for the tag
   - Attach `FAP-Client-win-x64-Release-*.zip` and `FAP-Server-win-x64-Release-*.zip`

3. **Release artifacts:** Find the release at `https://github.com/[owner]/[repo]/releases/tag/v1.2.3`

**Tag format:** Use `v*` tags (e.g., `v1.0.0`, `v1.2.3-beta`, `v2.0.0-rc1`).

### Local Build (Manual)

#### Quick start (Client only)

```powershell
pwsh -File .\build.ps1 -Configuration Release -Runtime win-x64
```

Outputs:
- artifacts/publish/client/win-x64/Release/ (published files)
- artifacts/release/FAP-Client-win-x64-Release-YYYYMMDD-HHMM.zip (packaged zip)

#### Include Server package

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
- The CI workflow uses the same `build.ps1` script to ensure consistency between local and automated builds.


