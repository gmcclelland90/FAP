Param(
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [switch]$IncludeServer,
  [switch]$RunTests
)

$ErrorActionPreference = 'Stop'

function Publish-Project {
  param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$Output
  )

  Write-Host "Publishing $Project → $Output" -ForegroundColor Cyan
  $publishArgs = @(
    'publish', $Project,
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:PublishReadyToRun=true',
    '-o', $Output
  )
  & dotnet @publishArgs
}

function Zip-Output {
  param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][string]$ZipPath
  )
  Write-Host "Zipping $SourceDir → $ZipPath" -ForegroundColor Cyan
  if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
  $null = New-Item -ItemType Directory -Path (Split-Path $ZipPath) -Force
  Compress-Archive -Path (Join-Path $SourceDir '*') -DestinationPath $ZipPath -Force
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmm'

# Build solution (ensures tests and server artifacts are ready)
Write-Host "Building solution ($Configuration)" -ForegroundColor Cyan
& dotnet build (Join-Path $PSScriptRoot 'Fap.sln') -c $Configuration | Out-Host

# Publish WPF client
$clientProj = Join-Path $PSScriptRoot 'UI/Client.WPF/Fap.Presentation.csproj'
$clientOut  = Join-Path $PSScriptRoot "artifacts/publish/client/$Runtime/$Configuration"
Publish-Project -Project $clientProj -Output $clientOut

$artifactsDir = Join-Path $PSScriptRoot 'artifacts/release'
$clientZip = Join-Path $artifactsDir "FAP-Client-$Runtime-$Configuration-$timestamp.zip"
Zip-Output -SourceDir $clientOut -ZipPath $clientZip

if ($IncludeServer) {
  $serverProj = Join-Path $PSScriptRoot 'UI/Server.Console/Server.Console.csproj'
  $serverOut  = Join-Path $PSScriptRoot "artifacts/publish/server/$Runtime/$Configuration"
  Publish-Project -Project $serverProj -Output $serverOut

  $serverZip = Join-Path $artifactsDir "FAP-Server-$Runtime-$Configuration-$timestamp.zip"
  Zip-Output -SourceDir $serverOut -ZipPath $serverZip
}

Write-Host "Done." -ForegroundColor Green
Write-Host "Client publish: $clientOut"
Write-Host "Client zip: $clientZip"
if ($IncludeServer) { Write-Host "Server: $serverZip" }

if ($RunTests) {
  Write-Host "Running integration tests..." -ForegroundColor Cyan
  $serverExe = Join-Path $PSScriptRoot "UI/Server.Console/bin/$Configuration/net9.0-windows/win-x64/Server.Console.exe"
  if (-not (Test-Path $serverExe)) {
    throw "Server executable not found at $serverExe. Build may have failed."
  }
  $env:FAP_SERVER_URL = 'http://127.0.0.1:4040'
  $args = @('--Fap:Web:Listen:Address','127.0.0.1','--Fap:Web:Listen:Port','4040')
  $proc = Start-Process -FilePath $serverExe -ArgumentList $args -PassThru
  try {
    # Simple wait; server also has internal readiness quickly
    Start-Sleep -Seconds 3
    & dotnet test (Join-Path $PSScriptRoot 'tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj') -c $Configuration --no-build --logger 'trx;LogFileName=TestResults.trx' | Out-Host
    $testCode = $LASTEXITCODE
  }
  finally {
    try { $proc.Kill() } catch {}
  }
  if ($testCode -ne 0) { throw "Integration tests failed with exit code $testCode" }
  Write-Host "Integration tests passed." -ForegroundColor Green
}


