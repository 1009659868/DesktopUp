#requires -Version 5.1
param([string]$PackageDirectory, [string[]]$Cases=@('before-open','before-save','cancel-open','cancel-save','center-open','center-save','exit-open','exit-save'))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=if ($PackageDirectory) { [IO.Path]::GetFullPath($PackageDirectory) } else { Join-Path $taskRepo 'out\verify\v3-04-06-picker-r3' }
$taskAllowed=@('before-open','before-save','cancel-open','cancel-save','center-open','center-save','exit-open','exit-save','cancel-folder','center-folder','exit-folder','zero-folder')
if (!$Cases.Count -or @($Cases | Where-Object { $_ -notin $taskAllowed }).Count) { throw 'Unknown native picker case.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first. No process was stopped.' }
$taskExe=Join-Path $taskPackage 'app\DesktopUp.exe'
$taskFixture=Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe'
$taskHash=(Get-FileHash (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash
$taskNativeHash=(Get-FileHash (Join-Path $taskPackage 'app\DesktopUp.Windows.dll')).Hash
$taskRoot=Join-Path $taskPackage ('picker-gates-' + [Guid]::NewGuid().ToString('N'))
$taskResults=@()
foreach ($taskCase in $Cases) {
    $taskData=Join-Path $taskRoot $taskCase
    $taskPosterCount=if($taskCase -eq 'zero-folder'){0}else{1}
    if($taskPosterCount -eq 0){ & $taskFixture $taskData 0 | Out-Null }
    else { & $taskFixture $taskData $taskPosterCount --measure-expanded | Out-Null }
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated picker fixture.' }
    $taskDb=Join-Path $taskData 'state\desktopup.sqlite3'
    $taskDbHash=(Get-FileHash -LiteralPath $taskDb).Hash
    $taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments ("--picker-smoke --picker-case=$taskCase --show-control-center") -WindowStyle Hidden
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(45)
    while (!$taskProcess.WaitForExit(500)) {
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw "Picker gate timed out; process $($taskProcess.Id) preserved. Close its dialog normally, do not force kill." }
    }
    $taskProcess.Refresh()
    $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $taskErrors=@($taskEvents | Where-Object { $_.level -in @('error','fatal') })
    $taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
    $taskExit=@($taskEvents | Where-Object eventName -eq 'application.exit_requested')
    $taskNative=@($taskEvents | Where-Object eventName -eq 'portable_picker.lifecycle')
    $taskCompleted=@($taskNative | Where-Object { $_.data.stage -eq 'completed' })
    $taskShown=@($taskNative | Where-Object { $_.data.stage -eq 'shown' })
    $taskExpected=if ($taskCase.StartsWith('cancel-')) {3} else {1}
    if ($taskErrors.Count -or $taskProcess.ExitCode -ne 0 -or $taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0 -or
        $taskExit.Count -ne 1 -or $taskCompleted.Count -ne $taskExpected -or
        @($taskCompleted | Where-Object { !$_.data.detail.cancelled -or $_.data.detail.dialogAlive -or $_.data.detail.failed }).Count) {
        throw "Failed native picker case $taskCase; evidence preserved: $taskData"
    }
    if (!$taskCase.StartsWith('before-') -and $taskShown.Count -ne $taskExpected) { throw 'Actual native dialog appearance was not proved.' }
    if ($taskCase.StartsWith('center-')) {
        $taskCenterCancel=@($taskEvents | Where-Object { $_.eventName -eq 'portable.cancel_requested' -and $_.data.picker -and $_.data.busy })
        if ($taskCenterCancel.Count -ne 1) { throw 'Center close did not traverse production cancellation.' }
    }
    $taskExitMs=([DateTimeOffset]::Parse($taskStopped[0].timestampUtc)-[DateTimeOffset]::Parse($taskExit[0].timestampUtc)).TotalMilliseconds
    if ($taskCase.StartsWith('exit-')) {
        $taskArmed=@($taskEvents | Where-Object eventName -eq 'contract.picker_exit_armed')
        $taskClosed=@($taskNative | Where-Object { $_.data.stage -eq 'close_requested' })
        if ($taskArmed.Count -ne 1 -or !$taskClosed.Count -or $taskExitMs -gt 5000) { throw 'Native exit was not armed/closed/completed promptly.' }
    } else {
        $taskDone=@($taskEvents | Where-Object eventName -eq 'contract.picker_completed')
        if ($taskDone.Count -ne 1 -or !$taskDone[0].data.passed) { throw 'Native picker completion proof missing.' }
    }
    if ((Get-FileHash -LiteralPath $taskDb).Hash -ne $taskDbHash) { throw 'Picker-only regression unexpectedly changed database bytes.' }
    if ((Get-FileHash (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash -ne $taskHash -or
        (Get-FileHash (Join-Path $taskPackage 'app\DesktopUp.Windows.dll')).Hash -ne $taskNativeHash) { throw 'Candidate changed during native gate.' }
    $taskResult=[pscustomobject]@{Result='PASS - REAL NATIVE PICKER / PROGRAMMATIC COMMAND'; Case=$taskCase; ProcessId=$taskProcess.Id;
        ExitCode=$taskProcess.ExitCode; ResourcesAfterExit=$taskStopped[0].data.activeResources; ExitMs=$taskExitMs;
        NativePicker=$true; NativeMouse=$false; FileIO=$false; DatabaseUnchanged=$true;
        AppHash=$taskHash; WindowsHash=$taskNativeHash; DataRoot=$taskData}
    $taskResults+=$taskResult
    $taskResult
}
$taskResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot 'results.json') -Encoding UTF8
if (($Cases | Sort-Object -Unique).Count -eq 8 -and $taskResults.Count -eq 8) {
    $taskResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskPackage 'picker-gates-passed.json') -Encoding UTF8
}
