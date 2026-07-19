Param(
  [string]$BaseUrl = '',
  [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outDir = Join-Path $repoRoot "artifacts/agent-feedback/guest/$timestamp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$sha = (git rev-parse --short HEAD 2>$null)
if (-not $sha) { $sha = 'unknown' }

@"
# Guest web agent feedback pack

- Timestamp: $timestamp
- Commit: $sha
- BaseUrl: $(if ($BaseUrl) { $BaseUrl } else { '(fixture-hosted by capture)' })
- Steps: root; search (?q=); Open in FAP opt-in; narrow; empty; path-error; busy; dark
- Expected: attach this folder when asking an agent to debug guest UI
"@ | Set-Content -Path (Join-Path $outDir 'meta.md') -Encoding UTF8

Write-Host "Building guest UI test host..." -ForegroundColor Cyan
dotnet build (Join-Path $repoRoot 'tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj') -c $Configuration | Out-Host

$install = Join-Path $repoRoot 'tests/FAP.GuestWeb.UITests/install-playwright.ps1'
if (Test-Path $install) { & $install }

# Capture while the GuestHostFixture is alive (HTML/a11y + multi-state screenshots).
$env:FAP_CAPTURE_DIR = $outDir
dotnet test (Join-Path $repoRoot 'tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj') -c $Configuration --filter "FullyQualifiedName~Capture_agent_feedback_pack" --logger "console;verbosity=detailed" | Tee-Object -FilePath (Join-Path $outDir 'test-log.txt') | Out-Host

# Back-compat: also keep guest-root.png copy if the dedicated pack wrote page.png
$shotSrc = Join-Path $repoRoot 'tests/FAP.GuestWeb.UITests/screenshots/guest-root.png'
if ((-not (Test-Path (Join-Path $outDir 'page.png'))) -and (Test-Path $shotSrc)) {
  Copy-Item $shotSrc (Join-Path $outDir 'page.png') -Force
}

$auditSrc = Join-Path $repoRoot 'artifacts/agent-feedback/guest/AUDIT.md'
if (Test-Path $auditSrc) {
  Copy-Item $auditSrc (Join-Path $outDir 'AUDIT.md') -Force
}

# Optional override: if caller passed a live BaseUrl, refresh root HTML from that host
if ($BaseUrl) {
  try {
    $url = $BaseUrl.TrimEnd('/') + '/'
    $html = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).Content
    $html | Set-Content -Path (Join-Path $outDir 'page-live.html') -Encoding UTF8
  } catch {
    "Live HTML dump skipped: $($_.Exception.Message)" | Set-Content -Path (Join-Path $outDir 'page-live.html') -Encoding UTF8
  }
}

$expected = @(
  'page.png', 'page.html', 'a11y.json', 'search.png', 'optin-on.png', 'narrow-optin.png',
  'empty.png', 'path-error.png', 'busy.png', 'dark.png', 'AUDIT.md'
)
foreach ($name in $expected) {
  $p = Join-Path $outDir $name
  if (Test-Path $p) { Write-Host "  ok $name" -ForegroundColor DarkGreen }
  else { Write-Host "  missing $name" -ForegroundColor Yellow }
}

Write-Host "Agent feedback pack: $outDir" -ForegroundColor Green
Write-Host "Attach this folder when asking an agent to debug guest UI."
