Param(
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [switch]$IncludeServer,
  [switch]$RunTests,
  [switch]$RunUiTests
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

# Publish WinUI client (primary); WPF remains at UI/Client.WPF for legacy builds
$clientProj = Join-Path $PSScriptRoot 'UI/Client.WinUI/Client.WinUI.csproj'
$clientOut  = Join-Path $PSScriptRoot "artifacts/publish/client/$Runtime/$Configuration"
Write-Host "Publishing $clientProj → $clientOut" -ForegroundColor Cyan
& dotnet publish $clientProj -c $Configuration -r $Runtime --self-contained true -p:Platform=x64 -o $clientOut | Out-Host

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
  Write-Host "Running unit + integration + guest UI tests..." -ForegroundColor Cyan
  $env:DOTNET_ROLL_FORWARD = 'LatestMajor'
  & dotnet test (Join-Path $PSScriptRoot 'tests/FAP.UnitTests/FAP.UnitTests.csproj') -c $Configuration --logger 'trx;LogFileName=UnitTests.trx' | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "Unit tests failed with exit code $LASTEXITCODE" }
  & dotnet test (Join-Path $PSScriptRoot 'tests/FAP.IntegrationTests/FAP.IntegrationTests.csproj') -c $Configuration --logger 'trx;LogFileName=IntegrationTests.trx' | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "Integration tests failed with exit code $LASTEXITCODE" }
  $guestProj = Join-Path $PSScriptRoot 'tests/FAP.GuestWeb.UITests/FAP.GuestWeb.UITests.csproj'
  & dotnet build $guestProj -c $Configuration | Out-Host
  & pwsh -File (Join-Path $PSScriptRoot 'tests/FAP.GuestWeb.UITests/install-playwright.ps1') | Out-Host
  & dotnet test $guestProj -c $Configuration --logger 'trx;LogFileName=GuestWebUITests.trx' | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "Guest web UI tests failed with exit code $LASTEXITCODE" }
  Write-Host "Unit, integration, and guest UI tests passed." -ForegroundColor Green
}

if ($RunUiTests) {
  Write-Host "Running WinUI FlaUI tests (Category=UI)..." -ForegroundColor Cyan
  $env:DOTNET_ROLL_FORWARD = 'LatestMajor'
  & dotnet build (Join-Path $PSScriptRoot 'UI/Client.WinUI/Client.WinUI.csproj') -c $Configuration -p:Platform=x64 | Out-Host
  & dotnet test (Join-Path $PSScriptRoot 'tests/FAP.WinUI.UITests/FAP.WinUI.UITests.csproj') -c $Configuration --filter 'Category=UI' --logger 'trx;LogFileName=WinUiUITests.trx' | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "WinUI UI tests failed with exit code $LASTEXITCODE" }
  Write-Host "WinUI UI tests passed." -ForegroundColor Green
}


