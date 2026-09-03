param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $root 'tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj') `
    -c $Configuration `
    -p:Platform=x64
