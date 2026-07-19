Param(
  [string]$ExePath = '',
  [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outDir = Join-Path $repoRoot "artifacts/agent-feedback/winui/$timestamp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$sha = (git rev-parse --short HEAD 2>$null)
if (-not $sha) { $sha = 'unknown' }

Write-Host "Building WinUI client (x64 $Configuration)..." -ForegroundColor Cyan
dotnet build (Join-Path $repoRoot 'UI/Client.WinUI/Client.WinUI.csproj') -c $Configuration -p:Platform=x64 | Out-Host

if ($ExePath) { $env:FAP_WINUI_EXE = $ExePath }

@"
# WinUI agent feedback pack

- Timestamp: $timestamp
- Commit: $sha
- Exe: $(if ($env:FAP_WINUI_EXE) { $env:FAP_WINUI_EXE } else { '(default Debug x64 path)' })
- Steps: launch shell, navigate Home/Browse/Chat/Search/Queue/Shares/Compare/Settings, dump per-page PNG + uia-{label}.json
- Expected: attach this folder when asking an agent to debug WinUI
"@ | Set-Content -Path (Join-Path $outDir 'meta.md') -Encoding UTF8

$env:FAP_CAPTURE_DIR = $outDir
dotnet test (Join-Path $repoRoot 'tests/FAP.WinUI.UITests/FAP.WinUI.UITests.csproj') -c $Configuration --filter "Category=UI" --logger "console;verbosity=detailed" |
  Tee-Object -FilePath (Join-Path $outDir 'test-log.txt') | Out-Host

# Best-effort: if capture helper wrote files under FAP_CAPTURE_DIR from tests, keep them.
# Also write a stub uia note for agents when FlaUI dump is unavailable.
if (-not (Test-Path (Join-Path $outDir 'uia.json'))) {
  '{"note":"Run ShellNavigationTests locally; expand with FlaUI tree dump as needed."}' |
    Set-Content -Path (Join-Path $outDir 'uia.json') -Encoding UTF8
}

Write-Host "Agent feedback pack: $outDir" -ForegroundColor Green
Write-Host "Attach this folder when asking an agent to debug WinUI."
