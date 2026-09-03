param(
    [switch]$ShowControlCenter
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\DesktopUp.App\bin\x64\Release\net10.0-windows10.0.22621.0\win-x64\DesktopUp.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}
$arguments = @()
if ($ShowControlCenter) {
    $arguments += '--show-control-center'
}
if ($arguments.Count -gt 0) {
    Start-Process -FilePath $exe -ArgumentList $arguments
} else {
    Start-Process -FilePath $exe
}
