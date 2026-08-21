#Requires -Version 5.1
<#
.SYNOPSIS
  Run net9 time-to-connected benchmarks and optionally time legacy (.NET Framework) Server.Console.

.DESCRIPTION
  Always runs:
    dotnet test --filter FullyQualifiedName~TimeToConnected
  Artifacts land in artifacts/connect-timing/latest-net9-*.json
  (includes multi_cold_start: N clients cold-start race)

  Optional legacy A/B (dedicated-style startup):
    Set FAP_LEGACY_ROOT (or -LegacyRoot) to a clone of https://github.com/Kayomani/FAP.git
    Builds UI/Server.Console and times process start → "Server started" on stdout.
    Writes artifacts/connect-timing/latest-net40-dedicated.json (totalMs only; phases empty).

.PARAMETER LegacyRoot
  Path to Framework FAP checkout. Defaults to $env:FAP_LEGACY_ROOT.

.PARAMETER SkipNet9
  Skip the net9 xUnit TimeToConnected run.

.PARAMETER SkipLegacy
  Skip legacy Server.Console timing even if LegacyRoot is set.

.PARAMETER Configuration
  Build/test configuration (default Debug).
#>
param(
    [string]$LegacyRoot = $env:FAP_LEGACY_ROOT,
    [switch]$SkipNet9,
    [switch]$SkipLegacy,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$outDir = Join-Path $repoRoot "artifacts\connect-timing"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Write-TimingJson {
    param(
        [string]$Scenario,
        [string]$Runtime,
        [long]$TotalMs,
        [long]$BudgetMs,
        [hashtable]$Phases = @{},
        [hashtable]$Metrics = @{}
    )
    $obj = [ordered]@{
        version  = 1
        scenario = $Scenario
        runtime  = $Runtime
        totalMs  = $TotalMs
        budgetMs = $BudgetMs
        phases   = $Phases
        metrics  = $Metrics
    }
    $json = $obj | ConvertTo-Json -Depth 6
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $stamped = Join-Path $outDir "$Runtime-$Scenario-$stamp.json"
    $latest = Join-Path $outDir "latest-$Runtime-$Scenario.json"
    Set-Content -Path $stamped -Value $json -Encoding UTF8
    Set-Content -Path $latest -Value $json -Encoding UTF8
    Write-Host "[bench] wrote $latest totalMs=$TotalMs"
}

if (-not $SkipNet9) {
    Write-Host "[bench] Running net9 TimeToConnected tests..."
    $env:DOTNET_ROLL_FORWARD = "LatestMajor"
    dotnet test (Join-Path $repoRoot "tests\FAP.IntegrationTests\FAP.IntegrationTests.csproj") `
        -c $Configuration `
        --filter "FullyQualifiedName~TimeToConnected"
    if ($LASTEXITCODE -ne 0) {
        throw "net9 TimeToConnected tests failed (exit $LASTEXITCODE)"
    }
}

if (-not $SkipLegacy) {
    if ([string]::IsNullOrWhiteSpace($LegacyRoot)) {
        Write-Host "[bench] FAP_LEGACY_ROOT / -LegacyRoot not set; skipping Framework timing."
        Write-Host "[bench] Clone https://github.com/Kayomani/FAP.git and re-run with -LegacyRoot <path>"
    }
    else {
        $LegacyRoot = (Resolve-Path $LegacyRoot).Path
        Write-Host "[bench] Legacy root: $LegacyRoot"

        $sln = Join-Path $LegacyRoot "Fap.sln"
        if (-not (Test-Path $sln)) {
            throw "Legacy root missing Fap.sln: $LegacyRoot"
        }

        $msbuild = $null
        foreach ($candidate in @(
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
                "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
            )) {
            if (Test-Path $candidate) { $msbuild = $candidate; break }
        }
        if (-not $msbuild) {
            throw "MSBuild.exe not found. Install Visual Studio Build Tools with .NET Framework targeting pack."
        }

        $serverProj = Join-Path $LegacyRoot "UI\Server.Console\Server.Console.csproj"
        Write-Host "[bench] Building legacy Server.Console with $msbuild ..."
        & $msbuild $serverProj /p:Configuration=$Configuration /v:m
        if ($LASTEXITCODE -ne 0) {
            throw "Legacy Server.Console build failed"
        }

        $exeCandidates = @(
            (Join-Path $LegacyRoot "UI\Server.Console\bin\$Configuration\Server.Console.exe"),
            (Join-Path $LegacyRoot "UI\Server.Console\bin\x86\$Configuration\Server.Console.exe"),
            (Join-Path $LegacyRoot "UI\Server.Console\bin\x64\$Configuration\Server.Console.exe")
        )
        $exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $exe) {
            throw "Could not find Server.Console.exe under $LegacyRoot\UI\Server.Console\bin"
        }

        # Ensure port 40 free
        try {
            $r = Invoke-WebRequest -Uri "http://127.0.0.1:40/Fap.api/health" -TimeoutSec 1 -UseBasicParsing
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) {
                throw "Port 40 already in use. Stop running FAP before legacy bench."
            }
        }
        catch {
            if ($_.Exception.Message -match "already in use") { throw }
            # refused = ok
        }

        Write-Host "[bench] Starting legacy Server.Console: $exe"
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $exe
        $psi.WorkingDirectory = Split-Path $exe -Parent
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true

        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $psi
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        [void]$proc.Start()

        $deadline = [datetime]::UtcNow.AddSeconds(60)
        $sawStarted = $false
        while ([datetime]::UtcNow -lt $deadline -and -not $proc.HasExited) {
            while (-not $proc.StandardOutput.EndOfStream) {
                $line = $proc.StandardOutput.ReadLine()
                if ($null -eq $line) { break }
                Write-Host "[legacy] $line"
                if ($line -match "Server started") {
                    $sawStarted = $true
                    break
                }
            }
            if ($sawStarted) { break }
            Start-Sleep -Milliseconds 50
        }
        $sw.Stop()

        if (-not $sawStarted) {
            try { $proc.Kill() } catch { }
            throw "Legacy Server.Console did not print 'Server started' within 60s"
        }

        Write-TimingJson -Scenario "dedicated" -Runtime "net40" -TotalMs $sw.ElapsedMilliseconds -BudgetMs 15000

        try { $proc.Kill() } catch { }
        try { $proc.WaitForExit(5000) } catch { }
        Start-Sleep -Seconds 1
    }
}

Write-Host "[bench] Done. Compare with: .\scripts\compare-connect-timing.ps1"
