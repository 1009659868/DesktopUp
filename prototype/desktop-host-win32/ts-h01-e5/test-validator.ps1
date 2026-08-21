[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'validate-desktop-host-events.ps1'
$passSample = Join-Path $PSScriptRoot 'samples\pass-minimal.jsonl'
$failSample = Join-Path $PSScriptRoot 'samples\fail-late-frame.jsonl'

Write-Host 'Running known-good fixture...'
& $validator -Path $passSample
$passExit = $LASTEXITCODE

Write-Host 'Running known-bad stale-frame fixture...'
& $validator -Path $failSample
$failExit = $LASTEXITCODE

if ($passExit -ne 0) {
    Write-Error "Known-good fixture failed with exit code $passExit"
    exit 1
}
if ($failExit -eq 0) {
    Write-Error 'Known-bad fixture was incorrectly accepted'
    exit 1
}

Write-Host 'PASS: positive fixture accepted and stale-frame fixture rejected.'
exit 0
