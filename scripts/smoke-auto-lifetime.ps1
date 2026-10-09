#requires -Version 5.1
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot,
    [ValidateSet('exercise','enabled-exit','restart','continuous')][string]$Mode = 'exercise')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskRoot = (Resolve-Path -LiteralPath $DataRoot).Path.TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated lifecycle fixture, never the daily profile.'
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally first.' }
$taskLog = Join-Path $taskRoot 'logs\desktopup.jsonl'
$taskBefore = $null
if ($Mode -eq 'restart') {
    $taskOld = @(Get-Content -LiteralPath $taskLog | ConvertFrom-Json)
    $taskPrevious = @($taskOld | Where-Object { $_.eventName -eq 'auto_lifetime.smoke_completed' -and $_.data.passed }) | Select-Object -Last 1
    if (-not $taskPrevious) { throw 'Restart requires a completed lifecycle run in this isolated profile.' }
    $taskBefore = @($taskOld | Where-Object { $_.processId -eq $taskPrevious.processId -and $_.eventName -eq 'auto_lifetime.snapshot' }) | Select-Object -Last 1
}
$taskDeadline = if ($Mode -eq 'continuous') { 180000 } else { 28000 }
# The generic startup exit timer clamps to 60s. Continuous has its own cancellable
# 175s deadline; a second 60s timer would cut a valid six-poster run in half.
$taskArguments = "--auto-lifetime-smoke --lifetime-mode=$Mode"
if ($Mode -ne 'continuous') { $taskArguments += " --exit-after-ms=$taskDeadline" }
$taskProbe = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskRoot -Arguments $taskArguments -WindowStyle Hidden
if (-not $taskProbe.WaitForExit($taskDeadline + 5000)) { throw "Process did not exit normally: PID $($taskProbe.Id); do not force termination." }
$taskProbe.Refresh()
$taskEvents = @(Get-Content -LiteralPath $taskLog | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
$taskDone = @($taskEvents | Where-Object { $_.eventName -eq 'auto_lifetime.smoke_completed' -and $_.data.passed })
$taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
$taskLedger = Get-Content -LiteralPath (Join-Path $taskRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
if ($taskProbe.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or $taskErrors.Count -gt 0 -or -not $taskLedger.clean -or @($taskLedger.resources).Count -gt 0) {
    throw "Lifecycle smoke failed: PID=$($taskProbe.Id), completed=$($taskDone.Count), errors=$($taskErrors.Count). Inspect $taskRoot"
}
$taskAfter = @($taskEvents | Where-Object eventName -eq 'auto_lifetime.snapshot') | Select-Object -Last 1
$taskRounds = @($taskEvents | Where-Object eventName -eq 'auto_lifetime.round_completed')
if ($Mode -eq 'continuous' -and ($taskRounds.Count -ne 20 -or $taskDone[0].data.completed -ne 20 -or
    (Compare-Object @(1..20) @($taskRounds.data.completed)))) { throw 'Incomplete continuous rounds; not a PASS.' }
if ($Mode -eq 'restart') {
    $taskOldPosters = ConvertTo-Json -InputObject @($taskBefore.data.posters) -Depth 16 -Compress
    $taskNewPosters = ConvertTo-Json -InputObject @($taskAfter.data.posters) -Depth 16 -Compress
    if ($taskBefore.data.revision -ne $taskAfter.data.revision -or $taskOldPosters -cne $taskNewPosters) { throw 'Restart changed committed poster definitions/revision.' }
}
[pscustomobject]@{ Result='PASS'; Mode=$Mode; ProcessId=$taskProbe.Id; ExitCode=$taskProbe.ExitCode;
    InitialPosters=$taskDone[0].data.count; FinalPosters=@($taskAfter.data.posters).Count;
    ObservedOutside=$taskDone[0].data.observedOutside; NativeMessageFixture=($Mode -eq 'exercise' -and $taskDone[0].data.count -gt 0);
    ContinuousRounds=$taskRounds.Count; NativeInput=$false; LongRun=$false;
    ActiveResourcesAfterExit=@($taskLedger.resources).Count; DataRoot=$taskRoot }
