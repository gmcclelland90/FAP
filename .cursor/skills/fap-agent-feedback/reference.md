# FAP agent feedback — reference

## Capture script behavior

### `scripts/capture-guest-feedback.ps1`

1. Creates `artifacts/agent-feedback/guest/<timestamp>/` with `meta.md`
2. Builds `tests/FAP.GuestWeb.UITests` and runs Playwright Chromium install if needed
3. Sets `FAP_CAPTURE_DIR` and runs `Capture_agent_feedback_pack` (fixture stays up)
4. Writes while live: `page.png` + `page.html` + `a11y.json`, plus `search.png`, `optin-on.png`, `narrow-optin.png`
5. Optional `-BaseUrl` also writes `page-live.html` from an external host

Guest UI tests host a client listener on **8040** with share `Tmp` / `hello.txt`.

### `scripts/capture-winui-feedback.ps1`

1. Builds `UI/Client.WinUI` (x64)
2. Creates `artifacts/agent-feedback/winui/<timestamp>/` with `meta.md`
3. Sets `FAP_CAPTURE_DIR` and `FAP_WINUI_EXE` (optional `-ExePath`)
4. Runs FlaUI suite `Category=UI`
5. Shell test dumps `shell.png` + `uia.json` when `FAP_CAPTURE_DIR` is set

Default exe search order: `FAP_WINUI_EXE`, then `UI/Client.WinUI/bin/x64/Debug/.../Fap.Client.WinUI.exe`.

## Focused filters

```powershell
# Guest agent feedback pack (root + search + opt-in + narrow)
$env:FAP_CAPTURE_DIR = "artifacts/agent-feedback/guest/manual"
dotnet test tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj -c Debug --filter "FullyQualifiedName~Capture_agent_feedback_pack"

# Guest root screenshot only
dotnet test tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj -c Debug --filter "FullyQualifiedName~Root_screenshot_baseline"

# WinUI shell only
dotnet test tests/FAP.WinUI.UITests/FAP.WinUI.UITests.csproj -c Debug --filter "FullyQualifiedName~Shell_starts"
```

## Common failures

| Symptom | Likely cause | Action |
|---|---|---|
| Overlord bind fails on port 40 | Running `Fap.Client.WinUI` / overlord | Stop process; retry |
| Playwright browser missing | Browsers not installed | `install-playwright.ps1` |
| WinUI exe not found | Client not built x64 | `dotnet build UI/Client.WinUI/Client.WinUI.csproj -c Debug -p:Platform=x64` |
| Desktop runtime 9 missing | Machine has 8/10 only | `$env:DOTNET_ROLL_FORWARD='LatestMajor'` |
| FlaUI cannot find AutomationId | Stale build / wrong window | Rebuild WinUI; ensure one instance |

## What not to do

- Do not pixel-compare WinUI to legacy WPF
- Do not use a vision model as the sole CI pass/fail oracle
- Do not commit `artifacts/` (already gitignored)
- Do not run WinUI FlaUI on every PR unless asked (`build.ps1 -RunUiTests` / nightly)

## Related docs

- [docs/testing-platform.md](../../../docs/testing-platform.md) — must-not-break matrix, CI
- [TESTINGTODO.md](../../../TESTINGTODO.md) — remaining harness work
