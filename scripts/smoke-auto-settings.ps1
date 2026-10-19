#requires -Version 5.1
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskRoot = (Resolve-Path -LiteralPath $DataRoot).Path.TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a new isolated zero/three-poster fixture, never the daily profile.'
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally first.' }
$taskProbe = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskRoot -Arguments '--auto-settings-smoke --exit-after-ms=30000' -WindowStyle Hidden
if (-not $taskProbe.WaitForExit(35000)) { throw "Process did not exit normally: PID $($taskProbe.Id); do not force termination." }
$taskProbe.Refresh()
$taskEvents = @(Get-Content -LiteralPath (Join-Path $taskRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
$taskDone = @($taskEvents | Where-Object { $_.eventName -eq 'auto_settings.smoke_completed' -and $_.data.passed })
$taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
$taskLedger = Get-Content -LiteralPath (Join-Path $taskRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
if ($taskProbe.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or $taskErrors.Count -gt 0 -or -not $taskLedger.clean -or @($taskLedger.resources).Count -gt 0) {
    throw "Settings smoke failed: PID=$($taskProbe.Id), completed=$($taskDone.Count), errors=$($taskErrors.Count). Inspect $taskRoot"
}
[pscustomobject]@{ Result='PASS'; ProcessId=$taskProbe.Id; ExitCode=$taskProbe.ExitCode; XamlControls=$true;
    ActiveResourcesAfterExit=@($taskLedger.resources).Count; DataRoot=$taskRoot }
