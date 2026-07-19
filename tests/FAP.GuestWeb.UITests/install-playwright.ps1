$ErrorActionPreference = 'Stop'
$projDir = $PSScriptRoot
dotnet build (Join-Path $projDir 'FAP.GuestWeb.UITests.csproj') -c Debug | Out-Host
$script = Get-ChildItem -Path (Join-Path $projDir 'bin') -Filter 'playwright.ps1' -Recurse -ErrorAction SilentlyContinue |
  Select-Object -First 1
if (-not $script) {
  throw "playwright.ps1 not found under bin/. Ensure Microsoft.Playwright package is restored."
}
Write-Host "Running $($script.FullName) install chromium" -ForegroundColor Cyan
& $script.FullName install chromium
Write-Host "Playwright Chromium install complete." -ForegroundColor Green
