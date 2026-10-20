#requires -Version 5.1
param(
    [switch]$ShowControlCenter,
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'out\run\app'
. (Join-Path $PSScriptRoot 'toolchain.ps1')
$runTools = Enter-DesktopUpToolchain -Offline:$Offline
try {
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -OutputDirectory $output -Offline:$Offline
$exe = Join-Path $output 'DesktopUp.exe'
$arguments = @()
if ($ShowControlCenter) {
    $arguments += '--show-control-center'
}
if ($arguments.Count -gt 0) {
    Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden
} else {
    Start-Process -FilePath $exe -WindowStyle Hidden
}
} finally { Exit-DesktopUpToolchain $runTools }
