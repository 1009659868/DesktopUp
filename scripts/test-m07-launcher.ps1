#requires -Version 5.1
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskScripts=@('Start-DesktopUpFixtureProcess.ps1','start-m07-manual.ps1','start-m07-measurement.ps1',
 'measure-m07.ps1','measure-m07-spatial.ps1','smoke-spatial.ps1','test-m07-launcher.ps1','Get-M07StableBuild.ps1',
 'smoke-input-confirmation.ps1','smoke-multi-instance.ps1','test-m07-safety.ps1')
foreach ($taskScript in $taskScripts) {
 $taskTokens=$null; $taskParseErrors=$null
 $null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $taskScript),[ref]$taskTokens,[ref]$taskParseErrors)
 if ($taskParseErrors.Count -gt 0) { throw ($taskParseErrors | Out-String) }
}
if (-not ('DesktopUpMeasurementMath' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'MeasurementMath.cs') }
if ([DesktopUpMeasurementMath]::Quantile([double[]]@(1,2,3,4),.95) -ne 4 -or
 [DesktopUpMeasurementMath]::CpuPercent(100,1000,4) -ne 2.5) { throw 'Statistics compatibility check failed.' }
foreach ($taskInvalid in @([double]::NaN,[double]::PositiveInfinity,[double]::NegativeInfinity)) {
 $taskRejected=$false
 try { $null=[DesktopUpMeasurementMath]::Quantile([double[]]@(1,$taskInvalid),.5) } catch { $taskRejected=$true }
 if (-not $taskRejected) { throw 'Non-finite sample was accepted.' }
}
foreach ($taskScenario in @('interaction','chain','return-sync','removal','blocked')) {
 $taskPreflight=& (Join-Path $PSScriptRoot 'start-m07-manual.ps1') -Scenario $taskScenario -CheckOnly
 if ($taskPreflight.VerifiedBinaries -ne 24 -or $taskPreflight.Launched) { throw 'Manual preflight failed.' }
}
$taskMeasurement=& (Join-Path $PSScriptRoot 'start-m07-measurement.ps1') -CheckOnly
if ($taskMeasurement.VerifiedBinaries -ne 24) { throw 'Measurement preflight failed.' }
& (Join-Path $PSScriptRoot 'measure-m07-spatial.ps1') -CheckOnly | Out-Null
if ($CheckOnly) { Write-Output 'STATIC PREFLIGHT PASS - no processes or fixtures created'; return }
$taskPreviousRoot=[Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT','Process')
$taskLaunch=& (Join-Path $PSScriptRoot 'start-m07-manual.ps1') -Scenario interaction -ExitAfterMs 8000
$taskProcess=[Diagnostics.Process]::GetProcessById($taskLaunch.ProcessId)
# Retain a handle before exit; .NET Framework cannot query an already-gone PID.
$null=$taskProcess.Handle
if (-not $taskProcess.WaitForExit(20000)) { throw "Timed test did not exit normally: PID $($taskLaunch.ProcessId). No forced termination." }
$taskEvents=@(Get-Content -LiteralPath $taskLaunch.LogPath | ConvertFrom-Json | Where-Object { $_.processId -eq $taskLaunch.ProcessId })
$taskReady=@($taskEvents | Where-Object eventName -eq 'poster_session.ready')
$taskCenters=@($taskEvents | Where-Object eventName -eq 'control_center.created')
$taskTrays=@($taskEvents | Where-Object eventName -eq 'tray.created')
$taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
$taskErrors=@($taskEvents | Where-Object level -in 'error','critical')
$taskLedger=Get-Content -LiteralPath (Join-Path $taskLaunch.DataRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
$taskParentUnchanged=$taskPreviousRoot -ceq [Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT','Process')
$taskPassed=$taskProcess.ExitCode -eq 0 -and $taskReady.Count -eq 3 -and $taskCenters.Count -eq 1 -and $taskTrays.Count -eq 1 -and
 $taskStopped.Count -eq 1 -and $taskErrors.Count -eq 0 -and $taskLedger.clean -and @($taskLedger.resources).Count -eq 0 -and $taskParentUnchanged
$taskEvidence=Join-Path $taskRepo ('out\verify\v3-04-m07-closeout\launcher-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
$null=New-Item -ItemType Directory -Path $taskEvidence
$taskReport=[ordered]@{ Passed=$taskPassed; PowerShell=$PSVersionTable.PSVersion.ToString(); Runtime=[Environment]::Version.ToString();
 Elevated=$taskLaunch.Elevated; EvidenceScope='current-environment'; ProcessId=$taskLaunch.ProcessId; ExitCode=$taskProcess.ExitCode;
 ScriptsParsed=$taskScripts.Count; ReadyPosters=$taskReady.Count; ControlCenters=$taskCenters.Count; Trays=$taskTrays.Count;
 StoppedEvents=$taskStopped.Count; ErrorEvents=$taskErrors.Count; LedgerClean=$taskLedger.clean; ResourcesAfterExit=@($taskLedger.resources).Count;
 ParentEnvironmentUnchanged=$taskParentUnchanged; LogPath=$taskLaunch.LogPath; LogHash=(Get-FileHash -LiteralPath $taskLaunch.LogPath).Hash;
 Limit='Automatic launch/clean-exit only. This probe alone does not certify manual S1-S5 or the full resource matrix.' }
$taskReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskEvidence 'result.json') -Encoding UTF8
$taskReport | ConvertTo-Json -Depth 6
Write-Output "Evidence: $taskEvidence"
$taskProcess.Dispose()
if (-not $taskPassed) { throw 'M07 launcher regression failed; inspect saved evidence.' }
