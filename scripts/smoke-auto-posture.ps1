#requires -Version 5.1
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskRoot = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated three-poster --measure-expanded fixture, never the daily profile.'
}
if (-not (Test-Path -LiteralPath $taskRoot -PathType Container)) { throw 'Create the fixture first.' }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally first.' }
$taskProbe = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskRoot -Arguments '--auto-posture-smoke --exit-after-ms=38000' -WindowStyle Hidden
if (-not $taskProbe.WaitForExit(43000)) { throw "Process did not exit normally: PID $($taskProbe.Id); inspect it without forcing termination." }
$taskProbe.Refresh()
$taskEvents = @(Get-Content -LiteralPath (Join-Path $taskRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
$taskDone = @($taskEvents | Where-Object { $_.eventName -eq 'auto_posture.smoke_completed' -and $_.data.passed })
$taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
$taskRequests = @($taskEvents | Where-Object eventName -eq 'auto_posture.request')
$taskCommits = @($taskEvents | Where-Object { $_.eventName -eq 'auto_posture.completed' -and $_.data.outcome -eq 'Committed' })
$taskCancelled = @($taskEvents | Where-Object { $_.eventName -eq 'auto_posture.completed' -and $_.data.outcome -eq 'Conflict' })
$taskLedger = Get-Content -LiteralPath (Join-Path $taskRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
if ($taskProbe.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or $taskErrors.Count -gt 0 -or $taskRequests.Count -ne 4 -or
    $taskCommits.Count -ne 3 -or $taskCancelled.Count -ne 1 -or -not $taskLedger.clean -or @($taskLedger.resources).Count -gt 0) {
    throw "Automatic smoke failed: PID=$($taskProbe.Id), completed=$($taskDone.Count), requests=$($taskRequests.Count), commits=$($taskCommits.Count), errors=$($taskErrors.Count). Inspect $taskRoot"
}
[pscustomobject]@{ Result='PASS'; ProcessId=$taskProbe.Id; NativePointer=$true; AutoRequests=$taskRequests.Count;
    AutoCommits=$taskCommits.Count; CancelledAfterPolicyChange=$taskCancelled.Count; ExitCode=$taskProbe.ExitCode;
    ActiveResourcesAfterExit=@($taskLedger.resources).Count; DataRoot=$taskRoot }
