#Requires -Version 5.1
<#
.SYNOPSIS
  Sweep FAP_TTC_DISCOVERY_GRACE_MS and record TimeToConnected results.

.DESCRIPTION
  For each graceMs value: run TimeToConnected with tight agent budgets (overridable),
  read latest-net9-*.json, append a CSV row. Exit non-zero if no value passes all checks.

.PARAMETER GraceMsValues
  Comma-separated or array of grace ms values. Default: 1000,500,200,100,50,0

.PARAMETER MultiRepeats
  FAP_TTC_MULTI_REPEATS (default 5). Use 1 for a faster exploratory sweep.

.PARAMETER Configuration
  Build/test configuration (default Debug).
#>
param(
    [object]$GraceMsValues = @(1000, 500, 200, 100, 50, 0),
    [int]$MultiRepeats = 5,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$outDir = Join-Path $repoRoot "artifacts\connect-timing"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$csvPath = Join-Path $outDir "sweep-discovery-grace.csv"

if ($GraceMsValues -is [string]) {
    $GraceMsValues = $GraceMsValues -split ',' | ForEach-Object { [int]$_.Trim() }
}

$env:DOTNET_ROLL_FORWARD = "LatestMajor"
$env:FAP_TTC_BUDGET_MS_SOLO_ELECT = $(if ($env:FAP_TTC_BUDGET_MS_SOLO_ELECT) { $env:FAP_TTC_BUDGET_MS_SOLO_ELECT } else { "3000" })
$env:FAP_TTC_BUDGET_MS_JOIN_EXISTING = $(if ($env:FAP_TTC_BUDGET_MS_JOIN_EXISTING) { $env:FAP_TTC_BUDGET_MS_JOIN_EXISTING } else { "1500" })
$env:FAP_TTC_BUDGET_MS_DEDICATED = $(if ($env:FAP_TTC_BUDGET_MS_DEDICATED) { $env:FAP_TTC_BUDGET_MS_DEDICATED } else { "2500" })
$env:FAP_TTC_BUDGET_MS_MULTI_COLD_START = $(if ($env:FAP_TTC_BUDGET_MS_MULTI_COLD_START) { $env:FAP_TTC_BUDGET_MS_MULTI_COLD_START } else { "5000" })
$env:FAP_TTC_MULTI_REPEATS = "$MultiRepeats"

"graceMs,passed,soloMs,joinMs,multiMs,dedicatedMs,overlordCount,electAttempts,joinElectStart,notes" |
    Set-Content -Path $csvPath -Encoding UTF8

function Read-Json([string]$name) {
    $p = Join-Path $outDir "latest-net9-$name.json"
    if (-not (Test-Path $p)) { return $null }
    return Get-Content -Raw -Path $p | ConvertFrom-Json
}

$anyPass = $false
$bestGrace = $null

foreach ($grace in $GraceMsValues) {
    Write-Host ""
    Write-Host "========== sweep graceMs=$grace =========="
    $env:FAP_TTC_DISCOVERY_GRACE_MS = "$grace"

    $notes = ""
    $passed = $false
    $soloMs = ""; $joinMs = ""; $multiMs = ""; $dedMs = ""
    $olCount = ""; $electAttempts = ""; $joinElect = ""

    try {
        dotnet test (Join-Path $repoRoot "tests\FAP.IntegrationTests\FAP.IntegrationTests.csproj") `
            -c $Configuration `
            --filter "FullyQualifiedName~TimeToConnected" `
            --no-restore 2>&1 | Tee-Object -Variable testOut | Out-Host
        if ($LASTEXITCODE -ne 0) {
            # retry once with restore if build needed
            dotnet test (Join-Path $repoRoot "tests\FAP.IntegrationTests\FAP.IntegrationTests.csproj") `
                -c $Configuration `
                --filter "FullyQualifiedName~TimeToConnected" 2>&1 | Tee-Object -Variable testOut | Out-Host
        }

        $solo = Read-Json "solo_elect"
        $join = Read-Json "join_existing"
        $multi = Read-Json "multi_cold_start"
        $ded = Read-Json "dedicated"

        if ($solo) { $soloMs = $solo.totalMs }
        if ($join) {
            $joinMs = $join.totalMs
            $joinElect = [bool]($join.phases.PSObject.Properties.Name -contains "ElectStart")
        }
        if ($multi) {
            $multiMs = $multi.totalMs
            if ($multi.metrics.OverlordCount) { $olCount = $multi.metrics.OverlordCount }
            if ($multi.metrics.ElectAttempts) { $electAttempts = $multi.metrics.ElectAttempts }
        }
        if ($ded) { $dedMs = $ded.totalMs }

        $testFailed = $LASTEXITCODE -ne 0
        $olOk = ($olCount -eq 1 -or $olCount -eq "1")
        $joinOk = -not $joinElect

        if (-not $testFailed -and $olOk -and $joinOk) {
            $passed = $true
            $anyPass = $true
            $bestGrace = $grace
            $notes = "ok"
        }
        elseif ($testFailed) {
            $notes = "test_failed"
        }
        elseif (-not $olOk) {
            $notes = "overlord_count=$olCount"
        }
        elseif (-not $joinOk) {
            $notes = "join_self_elected"
        }
    }
    catch {
        $notes = $_.Exception.Message.Replace(",", ";")
    }

    $row = "$grace,$passed,$soloMs,$joinMs,$multiMs,$dedMs,$olCount,$electAttempts,$joinElect,$notes"
    Add-Content -Path $csvPath -Value $row -Encoding UTF8
    Write-Host "[sweep] $row"
}

Write-Host ""
Write-Host "[sweep] wrote $csvPath"
if ($anyPass) {
    Write-Host "[sweep] smallest passing graceMs in this run order's last pass: $bestGrace"
    Write-Host "[sweep] Prefer the smallest graceMs row with passed=True in the CSV."
    exit 0
}
else {
    Write-Host "[sweep] No graceMs value passed all checks."
    exit 1
}
