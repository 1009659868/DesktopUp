#requires -Version 5.1
param([int[]]$PosterCounts=@(0,1,3), [switch]$SkipCenter, [ValidateSet('global','layout')][string]$Candidate='global',
    [string]$PackageDirectory)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=if ($PackageDirectory) { [IO.Path]::GetFullPath($PackageDirectory) } else { Join-Path $taskRepo ('out\verify\v3-04-05-' + $Candidate) }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit existing DesktopUp normally first. No process was stopped.' }
if (@($PosterCounts | Where-Object { $_ -notin @(0,1,3) }).Count) { throw 'Only isolated 0/1/3 scenarios are supported.' }
$taskEvidence=Join-Path $taskPackage ('gates-' + [Guid]::NewGuid().ToString('N'))
$taskResults=@()
function Invoke-TrayGate {
    param([string]$Mode,[int]$Count,[string]$Data)
    $taskFlag=switch($Mode) { 'exercise' {'--tray-smoke'} 'restart' {'--tray-smoke --tray-restart'} 'controls' {'--control-center-smoke'} 'settings' {'--auto-settings-smoke'} }
    $taskEvent=switch($Mode) { 'controls' {'control_center.smoke_completed'} 'settings' {'auto_settings.smoke_completed'} default {'tray.smoke_completed'} }
    $taskLog=Join-Path $Data 'logs\desktopup.jsonl'
    $taskPreviousLines=if (Test-Path -LiteralPath $taskLog) { [IO.File]::ReadAllLines($taskLog).Count } else { 0 }
    $taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $Data -Arguments ($taskFlag + ' --exit-after-ms=70000') -WindowStyle Hidden
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(80)
    while (!$taskProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw "Timed out; process preserved. Exit normally: $($taskProcess.Id)" }
    }
    $taskEvents=@(Get-Content -LiteralPath $taskLog | Select-Object -Skip $taskPreviousLines | ForEach-Object { $_ | ConvertFrom-Json })
    $taskDone=@($taskEvents | Where-Object eventName -eq $taskEvent | Select-Object -Last 1)
    $taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped' | Select-Object -Last 1)
    if ($taskProcess.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or !$taskDone[0].data.passed -or
        $taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0) {
        throw "Failed $Mode/$Count : $Data ; $($taskDone[0].data.error)"
    }
    [pscustomobject]@{Result='PASS - AUTOMATED ONLY'; Mode=$Mode; Posters=$Count; ProcessId=$taskProcess.Id;
        ResourcesAfterExit=$taskStopped[0].data.activeResources; DataRoot=$Data; NativeMouse=$false;
        AppHash=(Get-FileHash -LiteralPath (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash;
        FixtureHash=(Get-FileHash -LiteralPath (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.dll')).Hash;
        AnimationHash=(Get-FileHash -LiteralPath (Join-Path $taskPackage 'app\config\poster-animation.json')).Hash}
}
foreach ($taskCount in $PosterCounts) {
    $taskModes=@('exercise')
    if (!$SkipCenter) { $taskModes += @('controls','settings') }
    foreach ($taskMode in $taskModes) {
        $taskData=Join-Path (Join-Path $taskEvidence $taskMode) ([string]$taskCount)
        if ($taskCount -eq 0) { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData 0 | Out-Null }
        else { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount --measure-expanded | Out-Null }
        if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
        $taskItem=Invoke-TrayGate $taskMode $taskCount $taskData
        $taskResults += $taskItem; $taskItem
        if ($taskMode -eq 'exercise') {
            $taskItem=Invoke-TrayGate 'restart' $taskCount $taskData
            $taskResults += $taskItem; $taskItem
        }
    }
}
# Generated evidence only; partial runs never unlock the manual candidate.
$taskResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskEvidence 'results.json') -Encoding UTF8
if (!$SkipCenter -and $taskResults.Count -eq 12 -and (($PosterCounts | Sort-Object -Unique) -join ',') -eq '0,1,3') {
    $taskResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskPackage 'gates-passed.json') -Encoding UTF8
}
