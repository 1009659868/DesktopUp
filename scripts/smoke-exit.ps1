$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\DesktopUp.App\bin\x64\Release\net10.0-windows10.0.22621.0\win-x64\DesktopUp.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}
$process = Start-Process -FilePath $exe -ArgumentList '--exit-after-ms=1500' -PassThru
if (-not $process.WaitForExit(15000)) {
    throw 'DesktopUp did not exit within the smoke timeout.'
}
if ($process.ExitCode -ne 0) {
    throw "DesktopUp smoke exited with code $($process.ExitCode)."
}
$ledgerPath = Join-Path $env:LOCALAPPDATA 'DesktopUp\V3\state\resource-ledger.json'
$ledger = Get-Content -Raw -LiteralPath $ledgerPath | ConvertFrom-Json
if (-not $ledger.clean -or @($ledger.resources).Count -ne 0) {
    throw 'DesktopUp resource ledger was not clean after exit.'
}
Write-Host 'V3-01 process smoke: PASS'
