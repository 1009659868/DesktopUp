param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet restore (Join-Path $root 'DesktopUp.sln') -p:Platform=x64
dotnet build (Join-Path $root 'DesktopUp.sln') -c $Configuration -p:Platform=x64 --no-restore
