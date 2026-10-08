#requires -Version 5.1
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskRoot = (Resolve-Path -LiteralPath $DataRoot).Path.TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated three-poster --spatial-chain fixture; never the daily profile.'
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally first.' }
$taskProbe = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskRoot -Arguments '--title-surface-smoke --exit-after-ms=25000' -WindowStyle Hidden
if (-not $taskProbe.WaitForExit(30000)) { throw "Fixture did not exit normally: PID $($taskProbe.Id); do not force termination." }
$taskProbe.Refresh()
$taskEvents = @(Get-Content -LiteralPath (Join-Path $taskRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
$taskDone = @($taskEvents | Where-Object { $_.eventName -eq 'title_surface.smoke_completed' -and $_.data.passed })
$taskCases = @($taskEvents | Where-Object eventName -eq 'title_surface.case_passed')
$taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
$taskLedger = Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'state\resource-ledger.json') | ConvertFrom-Json
$taskExpected = @('prepare-partial','submit-before','submit-partial','restore-partial','complete',
    'handoff-reuse','handoff-grow','translate-fast','prepare-region-reset','handoff-reset-reuse','handoff-reset-grow')
$taskResets = @($taskEvents | Where-Object eventName -eq 'title_surface.region_reset_injected')
if ($taskProbe.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or $taskCases.Count -ne $taskExpected.Count -or
    (Compare-Object $taskExpected @($taskCases.data.fault)) -or $taskResets.Count -ne 3 -or $taskErrors.Count -gt 0 -or
    -not $taskLedger.clean -or @($taskLedger.resources).Count -gt 0) {
    throw "Title surface component smoke failed: PID=$($taskProbe.Id), cases=$($taskCases.Count), errors=$($taskErrors.Count). Inspect $taskRoot"
}
[pscustomobject]@{ Result='PASS'; ProcessId=$taskProbe.Id; ExitCode=$taskProbe.ExitCode; Cases=$taskCases.data.fault;
    NativeInput=$false; Scope='R4 component + shared restore, NOT native title ownership/ingress';
    ActiveResourcesAfterExit=@($taskLedger.resources).Count; DataRoot=$taskRoot }
