#requires -Version 5.1
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'start-m07-measurement.ps1') -CheckOnly | Out-Null
if ($CheckOnly) { Write-Output 'PREFLIGHT PASS - no processes launched'; return }
$taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
$taskElevated=([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$taskIdentity.Dispose()
if (-not ('DesktopUpMeasurementMath' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'MeasurementMath.cs') }
$taskExe=Join-Path $taskRepo 'out\verify\v3-04-m07\app\DesktopUp.exe'
$taskFixture=Join-Path $taskRepo 'tests\DesktopUp.SessionFixture\bin\x64\Release\net10.0-windows10.0.22621.0\DesktopUp.SessionFixture.exe'
$taskRoot=Join-Path $taskRepo ('out\verify\v3-04-m07\spatial-current-env-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
foreach ($taskCount in @(1,3,6)) {
 if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first.' }
 $taskData=Join-Path $taskRoot "n$taskCount"
 & $taskFixture $taskData $taskCount '--measure-expanded' | Out-Null
 if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
 $taskResult=& (Join-Path $PSScriptRoot 'smoke-spatial.ps1') -ExecutablePath $taskExe -DataRoot $taskData -ExpectedExpansion Full
 $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskResult.ProcessId)
 $taskStarts=@($taskEvents | Where-Object eventName -eq 'spatial.posture_started')
 $taskDone=@($taskEvents | Where-Object eventName -eq 'spatial.probe_command_completed')
 $taskFrames=@($taskEvents | Where-Object eventName -eq 'spatial.probe_frame')
 if ($taskStarts.Count -ne 2*$taskCount -or $taskDone.Count -ne 2*$taskCount -or @($taskDone | Where-Object { $_.data.status -ne 'Committed' }).Count -gt 0) { throw 'Missing successful command timings.' }
 $taskTimings=[Collections.Generic.List[object]]::new()
 foreach ($taskStart in $taskStarts) {
  $taskFramesForId=@($taskFrames | Where-Object { $_.data.RequestId -eq $taskStart.data.RequestId } | Sort-Object { $_.data.index })
  if ($taskFramesForId.Count -ne $taskStart.data.frames -or @($taskFramesForId | Where-Object { -not $_.data.applied }).Count -gt 0) { throw 'Planned frame count differs from successful actual submissions.' }
  $taskIntervals=[Collections.Generic.List[double]]::new()
  for ($taskIndex=1; $taskIndex -lt $taskFramesForId.Count; $taskIndex++) {
   if ($taskFramesForId[$taskIndex].data.index -ne $taskIndex+1) { throw 'Non-contiguous actual frames.' }
   $taskIntervals.Add(1000*($taskFramesForId[$taskIndex].data.qpc-$taskFramesForId[$taskIndex-1].data.qpc)/$taskFramesForId[$taskIndex].data.frequency)
  }
  $taskCommand=@($taskDone | Where-Object { $_.data.RequestId -eq $taskStart.data.RequestId })
  if ($taskCommand.Count -ne 1) { throw 'Duplicate or missing command completion.' }
  $taskTimings.Add([pscustomobject]@{ RequestId=$taskStart.data.RequestId; PosterId=$taskStart.data.PosterId; Collapsed=$taskStart.data.collapsed;
   ActualCommittedFrames=$taskFramesForId.Count; PlannedFrames=$taskStart.data.frames; CommandToCallbackMs=$taskCommand[0].data.elapsedMs;
   SubmitIntervalMedianMs=[DesktopUpMeasurementMath]::Quantile($taskIntervals.ToArray(),.5);
   SubmitIntervalP95Ms=[DesktopUpMeasurementMath]::Quantile($taskIntervals.ToArray(),.95);
   PrepareAndCommitP95Ms=[DesktopUpMeasurementMath]::Quantile([double[]]$taskFramesForId.data.prepareAndCommitMs,.95) })
 }
 [ordered]@{ SchemaVersion=1; Count=$taskCount; ProcessId=$taskResult.ProcessId; Elevated=$taskElevated; EvidenceScope='current-environment'; PowerShell=$PSVersionTable.PSVersion.ToString(); Result='PASS'; Timings=$taskTimings.ToArray();
  Limit='Programmatic command/CPU submission timings with probe logging. Not screen present FPS, mouse latency or GPU timings.';
  ExitCode=$taskResult.ExitCode; ResourcesAfterExit=$taskResult.ActiveResourcesAfterExit;
  LogHash=(Get-FileHash -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl')).Hash } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskData 'timing.json') -Encoding utf8
 Write-Output "n$taskCount PASS PID=$($taskResult.ProcessId), commands=$($taskDone.Count), actual submissions=$($taskFrames.Count)"
}
Write-Output "TIMING COMPLETE: $taskRoot"
