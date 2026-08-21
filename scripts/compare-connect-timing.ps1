#Requires -Version 5.1
<#
.SYNOPSIS
  Diff net9 vs legacy (net40) connect-timing JSON artifacts by scenario.

.DESCRIPTION
  Reads artifacts/connect-timing/latest-net9-<scenario>.json and
  latest-net40-<scenario>.json (when present). Exits non-zero if net9
  totalMs is worse than legacy by more than -ThresholdPercent (default 10).

.PARAMETER ArtifactsDir
  Defaults to <repo>/artifacts/connect-timing

.PARAMETER ThresholdPercent
  Allowed relative regression of net9 vs legacy (default 10).

.PARAMETER Scenarios
  Scenarios to compare (default: dedicated, solo_elect, join_existing, connect_rtt)
#>
param(
    [string]$ArtifactsDir = "",
    [double]$ThresholdPercent = 10,
    [string[]]$Scenarios = @("dedicated", "solo_elect", "join_existing", "connect_rtt")
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir = Join-Path $repoRoot "artifacts\connect-timing"
}

if (-not (Test-Path $ArtifactsDir)) {
    throw "Artifacts dir not found: $ArtifactsDir — run .\scripts\bench-connect-timing.ps1 first"
}

function Read-Report([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    return Get-Content -Raw -Path $path | ConvertFrom-Json
}

$failed = $false
$compared = 0

foreach ($scenario in $Scenarios) {
    $net9Path = Join-Path $ArtifactsDir "latest-net9-$scenario.json"
    $legacyPath = Join-Path $ArtifactsDir "latest-net40-$scenario.json"
    $net9 = Read-Report $net9Path
    $legacy = Read-Report $legacyPath

    if (-not $net9) {
        Write-Host "[compare] skip $scenario (missing net9 artifact)"
        continue
    }
    if (-not $legacy) {
        Write-Host "[compare] skip $scenario (missing net40 artifact — set FAP_LEGACY_ROOT and re-bench)"
        continue
    }

    $compared++
    $n = [long]$net9.totalMs
    $l = [long]$legacy.totalMs
    $ratio = if ($l -gt 0) { ($n - $l) / [double]$l * 100.0 } else { 0 }
    $limit = $l * (1.0 + $ThresholdPercent / 100.0)

    Write-Host ("[compare] {0}: net9={1}ms net40={2}ms delta={3:N1}% (limit +{4}%)" -f `
            $scenario, $n, $l, $ratio, $ThresholdPercent)

    if ($net9.metrics) {
        foreach ($p in $net9.metrics.PSObject.Properties) {
            Write-Host ("           metric {0}={1}" -f $p.Name, $p.Value)
        }
    }

    if ($n -gt $limit) {
        Write-Host "[compare] FAIL $scenario: net9 slower than legacy beyond threshold" -ForegroundColor Red
        $failed = $true
    }
    else {
        Write-Host "[compare] OK   $scenario" -ForegroundColor Green
    }

    # Join vs solo ratio when both net9 artifacts exist
    if ($scenario -eq "solo_elect") {
        $join = Read-Report (Join-Path $ArtifactsDir "latest-net9-join_existing.json")
        if ($join -and $n -gt 0) {
            $j = [long]$join.totalMs
            $jr = $j / [double]$n
            Write-Host ("[compare] JoinVsSoloRatio (net9) = {0:N3} (join={1} solo={2})" -f $jr, $j, $n)
            if ($jr -gt 0.9) {
                Write-Host "[compare] WARN JoinVsSoloRatio ~1 — discovery may not be skipping election" -ForegroundColor Yellow
            }
        }
    }
}

if ($compared -eq 0) {
    Write-Host "[compare] No scenario pairs found to compare."
    exit 2
}

if ($failed) { exit 1 }
Write-Host "[compare] All compared scenarios within threshold."
exit 0
