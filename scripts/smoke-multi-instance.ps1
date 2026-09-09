#requires -Version 7.4
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot,
    [ValidateSet('Exercise','Restart')][string]$Phase = 'Exercise')
$ErrorActionPreference = 'Stop'
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskRoot = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a new isolated --multi-instance SessionFixture, never the normal user profile.'
}
if (-not (Test-Path -LiteralPath $taskRoot -PathType Container)) { throw 'Create the fixture first.' }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Close the existing DesktopUp normally before this smoke.' }
$taskLogPath = Join-Path $taskRoot 'logs\desktopup.jsonl'
$taskPrevious = $null
if ($Phase -eq 'Restart') {
    if (-not (Test-Path -LiteralPath $taskLogPath)) { throw 'Exercise evidence is required before Restart.' }
    $taskPriorEvents = @(Get-Content -LiteralPath $taskLogPath | ConvertFrom-Json)
    $taskSuccess = @($taskPriorEvents | Where-Object { $_.eventName -eq 'multi_instance.smoke_completed' -and $_.data.passed -and -not $_.data.restartOnly })
    if ($taskSuccess.Count -eq 0) { throw 'A successful Exercise run is required before Restart.' }
    $taskPrevious = @($taskPriorEvents | Where-Object { $_.processId -eq $taskSuccess[-1].processId -and $_.eventName -eq 'multi_instance.snapshot' -and $_.data.phase -eq 'final' })[-1]
    if ($null -eq $taskPrevious) { throw 'Missing final persisted catalog evidence.' }
}
$taskFlag = if ($Phase -eq 'Exercise') { '--multi-instance-smoke' } else { '--multi-instance-restart' }
$taskEnvironment = @{ DESKTOPUP_V3_TEST_DATA_ROOT = $taskRoot }
$taskProbe = Start-Process -FilePath $taskExe -ArgumentList @($taskFlag,'--show-control-center','--exit-after-ms=22000') `
    -WindowStyle Hidden -PassThru -Environment $taskEnvironment
$taskSecondaryId = $null
if ($Phase -eq 'Exercise') {
    # Wait for this PID's initial snapshot, then use the real single-instance activation path.
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(9)
    $taskReady = $false
    while ([DateTime]::UtcNow -lt $taskDeadline -and -not $taskProbe.HasExited) {
        if (Test-Path -LiteralPath $taskLogPath) {
            $taskReady = @(Get-Content -LiteralPath $taskLogPath -Tail 120 | ForEach-Object { try { $_ | ConvertFrom-Json } catch { } } |
                Where-Object { $_.processId -eq $taskProbe.Id -and $_.eventName -eq 'multi_instance.snapshot' -and $_.data.phase -eq 'initial' }).Count -gt 0
        }
        if ($taskReady) { break }
        Start-Sleep -Milliseconds 150
    }
    if ($taskReady) {
        $taskSecondary = Start-Process -FilePath $taskExe -ArgumentList '--show-control-center' -WindowStyle Hidden -PassThru -Environment $taskEnvironment
        $taskSecondaryId = $taskSecondary.Id
        if (-not $taskSecondary.WaitForExit(5000)) { throw "Secondary activation did not exit: $taskSecondaryId" }
        $taskSecondary.Refresh()
        if ($taskSecondary.ExitCode -ne 0) { throw 'Secondary activation failed.' }
    }
}
if (-not $taskProbe.WaitForExit(27000)) { throw "M06 smoke did not exit: PID $($taskProbe.Id)." }
$taskProbe.Refresh()
$taskEvents = @(Get-Content -LiteralPath $taskLogPath | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
$taskDone = @($taskEvents | Where-Object { $_.eventName -eq 'multi_instance.smoke_completed' -and $_.data.passed })
$taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
$taskSnapshots = @($taskEvents | Where-Object eventName -eq 'multi_instance.snapshot')
$taskLedger = Get-Content -LiteralPath (Join-Path $taskRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
$taskCenters = @($taskEvents | Where-Object eventName -eq 'control_center.created').Count
$taskTrays = @($taskEvents | Where-Object eventName -eq 'tray.created').Count
$taskCenterExpected = if ($Phase -eq 'Exercise') { 2 } else { 1 } # Sequential close/reopen, never two live centers.
if ($taskProbe.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or $taskErrors.Count -gt 0 -or $taskTrays -ne 1 -or $taskCenters -ne $taskCenterExpected -or
    -not $taskLedger.clean -or @($taskLedger.resources).Count -gt 0 -or ($Phase -eq 'Exercise' -and $null -eq $taskSecondaryId)) {
    throw "M06 smoke failed: PID=$($taskProbe.Id), exit=$($taskProbe.ExitCode), completed=$($taskDone.Count), errors=$($taskErrors.Count), centers=$taskCenters, trays=$taskTrays. Inspect $taskLogPath"
}
if ($Phase -eq 'Exercise') {
    $taskExpectedPhases = @('initial','edited-a','hidden-b','edited-hidden-b','reshown-b','stale-removal-ready','deleted-c','final-catalog','final')
    if (($taskSnapshots.data.phase -join ',') -ne ($taskExpectedPhases -join ',')) { throw 'Incomplete exercise phase evidence.' }
} else {
    $taskRestart = @($taskSnapshots | Where-Object { $_.data.phase -eq 'restart' })
    if ($taskRestart.Count -ne 1 -or
        (ConvertTo-Json -InputObject $taskPrevious.data.posters -Compress -Depth 20) -cne
        (ConvertTo-Json -InputObject $taskRestart[0].data.posters -Compress -Depth 20)) { throw 'Restart changed persisted IDs, geometry, parameters or visibility.' }
}
[pscustomobject]@{ Result='PASS'; Phase=$Phase; ProcessId=$taskProbe.Id; SecondaryProcessId=$taskSecondaryId;
    ExitCode=$taskProbe.ExitCode; Phases=$taskSnapshots.data.phase; TrayCreations=$taskTrays; SequentialControlCenterCreations=$taskCenters;
    FinalCatalogCount=@($taskSnapshots[-1].data.posters).Count; FinalNativeCount=@($taskSnapshots[-1].data.frames).Count;
    ActiveResourcesAfterExit=@($taskLedger.resources).Count; DataRoot=$taskRoot }
