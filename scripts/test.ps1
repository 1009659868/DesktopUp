#requires -Version 5.1
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'toolchain.ps1')
$testTools = Enter-DesktopUpToolchain -Offline:$Offline
Push-Location $root
try {
    $testOptions = @('-p:Platform=x64')
    if ($Offline) { $testOptions += @("-p:RestoreConfigFile=$($testTools.OfflineConfig)", '-p:NuGetAudit=false') }
    & $testTools.DotNet test (Join-Path $root 'tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj') -c $Configuration @testOptions
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
} finally { Pop-Location; Exit-DesktopUpToolchain $testTools }
