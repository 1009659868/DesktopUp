#requires -Version 5.1
param([ValidateSet('ux','slider')][string]$Candidate='ux')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackageName=if ($Candidate -eq 'slider') { 'v3-04-04-ux-slider' } else { 'v3-04-04-ux' }
$taskPackage=Join-Path $taskRepo ('out\verify\' + $taskPackageName)
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit existing DesktopUp normally first. No process was stopped.' }
$taskEvidence=Join-Path $taskPackage ('gates-' + [Guid]::NewGuid().ToString('N'))
$taskResults=@()
foreach ($taskMode in @('controls','settings')) {
 foreach ($taskCount in @(0,1,3)) {
    $taskData=Join-Path (Join-Path $taskEvidence $taskMode) ([string]$taskCount)
    if ($taskCount -eq 0) { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData 0 | Out-Null }
    else { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount --measure-expanded | Out-Null }
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
    $taskFlag=if ($taskMode -eq 'controls') {'--control-center-smoke'} else {'--auto-settings-smoke'}
    $taskEvent=if ($taskMode -eq 'controls') {'control_center.smoke_completed'} else {'auto_settings.smoke_completed'}
    $taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments ($taskFlag + ' --exit-after-ms=50000') -WindowStyle Hidden
    if (!$taskProcess.WaitForExit(55000)) { throw "Timed out; process preserved. Exit normally: $($taskProcess.Id)" }
    $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $taskResult=@($taskEvents | Where-Object eventName -eq $taskEvent)
    $taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
    if ($taskProcess.ExitCode -ne 0 -or $taskResult.Count -ne 1 -or !$taskResult[0].data.passed -or
        $taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0) { throw "Failed UX gate: $taskData" }
    $taskItem=[pscustomobject]@{Result='PASS - AUTOMATED ONLY'; Mode=$taskMode; Posters=$taskCount; ProcessId=$taskProcess.Id;
        ResourcesAfterExit=$taskStopped[0].data.activeResources; DataRoot=$taskData;
        AppHash=(Get-FileHash -LiteralPath (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash; NativeMouse=$false}
    $taskResults += $taskItem
    $taskItem
 }
}
# Generated evidence only; never changes ordinary settings or candidate executables.
$taskResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskPackage 'gates-passed.json') -Encoding UTF8
