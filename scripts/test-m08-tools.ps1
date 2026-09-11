#requires -Version 5.1
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$taskNames=@('Get-M08Build.ps1','Test-M08NativeEnvironment.ps1','start-m08-manual.ps1','measure-m08.ps1','test-m08-tools.ps1')
foreach($taskName in $taskNames) {
 $taskTokens=$null; $taskErrors=$null
 $null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $taskName),[ref]$taskTokens,[ref]$taskErrors)
 if($taskErrors.Count -gt 0){throw ($taskErrors | Out-String)}
}
if(-not ('DesktopUpAutoMeasurementRules' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'M08MeasurementRules.cs')}
if(-not [DesktopUpAutoMeasurementRules]::CountsMatch(3,'on-trigger',19,3,3,3,3) -or
 [DesktopUpAutoMeasurementRules]::CountsMatch(3,'on-trigger',19,0,0,0,0)){throw 'Measurement count contract failed.'}
foreach($taskScenario in @('basic','chain','constrained','blocked','removal')) {
 $taskResult=& (Join-Path $PSScriptRoot 'start-m08-manual.ps1') -Scenario $taskScenario -CheckOnly
 if($taskResult.Launched -or $taskResult.VerifiedArtifacts -ne 25){throw 'Manual preflight changed state.'}
}
$taskCheck=& (Join-Path $PSScriptRoot 'measure-m08.ps1') -CheckOnly
if($taskCheck.Launched -or $taskCheck.Build.VerifiedArtifacts -ne 25){throw 'Measurement preflight changed state.'}
if($CheckOnly){[pscustomobject]@{Result='TOOL PREFLIGHT PASS - not manual/performance acceptance';PowerShell=$PSVersionTable.PSVersion.ToString();Native=$taskCheck.Native};return}
$taskParentRoot=[Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT','Process')
$taskPreviousHash=$null
foreach($taskIteration in @(1,2)) {
 $taskLaunch=& (Join-Path $PSScriptRoot 'start-m08-manual.ps1') -Scenario basic -ExitAfterMs 5000
 $taskProcess=[Diagnostics.Process]::GetProcessById($taskLaunch.ProcessId)
 $null=$taskProcess.Handle
 if(-not $taskProcess.WaitForExit(12000)){throw "Do not kill PID $($taskProcess.Id); inspect normal exit."}
 $taskProcess.Refresh()
 $taskEvents=@(Get-Content -LiteralPath $taskLaunch.LogPath | ConvertFrom-Json | Where-Object processId -eq $taskLaunch.ProcessId)
 $taskLedger=Get-Content -LiteralPath (Join-Path $taskLaunch.DataRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
 $taskErrors=@($taskEvents | Where-Object level -in 'error','critical')
 $taskHash=(Get-FileHash -LiteralPath (Join-Path $taskLaunch.DataRoot 'state\desktopup.sqlite3')).Hash
 if($taskProcess.ExitCode -ne 0 -or $taskErrors.Count -gt 0 -or !$taskLedger.clean -or @($taskLedger.resources).Count -ne 0){throw 'Manual launcher process failed.'}
 if($null -ne $taskPreviousHash -and $taskPreviousHash -cne $taskHash){throw 'Normal restart changed fixture database.'}
 if([Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT','Process') -cne $taskParentRoot){throw 'Launcher modified parent environment.'}
 [pscustomobject]@{Result='LAUNCH/EXIT PASS - manual results pending';Iteration=$taskIteration;ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;
  PowerShell=$PSVersionTable.PSVersion.ToString();ResourcesAfterExit=@($taskLedger.resources).Count;DatabaseHash=$taskHash}
 $taskPreviousHash=$taskHash; $taskProcess.Dispose()
}
