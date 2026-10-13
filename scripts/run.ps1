param(
    [switch]$ShowControlCenter
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'out\run\app'
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -OutputDirectory $output
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
