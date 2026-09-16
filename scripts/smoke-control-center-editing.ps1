#requires -Version 5.1
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=Join-Path $taskRepo 'out\verify\v3-04-04-06'
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit existing DesktopUp normally first.' }
$taskEvidence=Join-Path $taskPackage ('gates-' + [Guid]::NewGuid().ToString('N'))
foreach ($taskCount in @(0,1,3)) {
    $taskData=Join-Path $taskEvidence ([string]$taskCount)
    if ($taskCount -eq 0) { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData 0 | Out-Null }
    else { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount --measure-expanded | Out-Null }
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
    $taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments '--control-center-smoke --exit-after-ms=50000' -WindowStyle Hidden
    if (!$taskProcess.WaitForExit(55000)) { throw "Timed out. Process preserved; exit normally: $($taskProcess.Id)" }
    $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $taskResult=@($taskEvents | Where-Object eventName -eq 'control_center.smoke_completed')
    $taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
    if ($taskProcess.ExitCode -ne 0 -or $taskResult.Count -ne 1 -or !$taskResult[0].data.passed -or
        $taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0) { throw "Failed control center gate: $taskData" }
    [pscustomobject]@{Result='PASS - AUTOMATED CONTROLS/R4 ONLY'; Posters=$taskCount; ProcessId=$taskProcess.Id;
        Opens=$taskResult[0].data.opens; ResourcesAfterExit=$taskStopped[0].data.activeResources; DataRoot=$taskData;
        AppHash=(Get-FileHash -LiteralPath (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash; NativeMouse=$false}
}
