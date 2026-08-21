## FAP Release Guide

This repo includes a PowerShell script to publish and package the Windows client (and optionally the console server).

### Requirements
- PowerShell 7+
- .NET 9 SDK

### Versioning Scheme

**FAP's major version tracks the .NET TFM:**

- **FAP 9.x** = .NET 9 (`migration/net9-upgrade` branch after merge)
  - First release: `v9.0.0`
  - Patches: `v9.0.1`, `v9.0.2`, etc.
  - Minor releases: `v9.1.0`, `v9.2.0`, etc.

- **FAP 10.x** = .NET 10 (future `migration/net10-upgrade` branch)
  - Pre-releases: `v10.0.0-beta`, `v10.0.0-beta.1`, `v10.0.0-rc1`
  - First release: `v10.0.0` (after merge and stabilization)

This contract keeps version numbers aligned with the underlying framework version.

### Creating an Official Release

To create and publish a release with GitHub artifacts:

1. **Tag the release version:**
   ```bash
   # For the first .NET 9 release:
   git tag v9.0.0
   git push origin v9.0.0
   
   # For subsequent patches or features:
   git tag v9.1.0
   git push origin v9.1.0
   ```

2. **Automated build:** The GitHub Actions workflow (`.github/workflows/release.yml`) will automatically:
   - Build both client and server artifacts using `build.ps1`
   - Create a GitHub Release for the tag
   - Attach `FAP-Client-win-x64-Release-*.zip` and `FAP-Server-win-x64-Release-*.zip`

3. **Release artifacts:** Find the release at `https://github.com/[owner]/[repo]/releases/tag/v9.0.0`

**Tag format:** Use `v<major>.<minor>.<patch>` where major matches the .NET TFM. Pre-releases use suffix notation (e.g., `v10.0.0-beta.1`).

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
- The WinUI client publish is self-contained for win-x64 (includes .NET runtime and Windows App SDK).
- The publish output is a multi-file layout (not single-file) with the executable and supporting assemblies.
- The server publish uses single-file packaging via `PublishSingleFile=true`.
- You can override Configuration/Runtime, e.g. `-Configuration Debug`.
- The CI workflow uses the same `build.ps1` script to ensure consistency between local and automated builds.


