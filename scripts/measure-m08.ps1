#requires -Version 5.1
param([ValidateNotNullOrEmpty()][ValidateSet(1,3,6)][int[]]$Counts=@(1,3,6), [ValidateRange(1,3)][int]$Repeats=2,
 [ValidateNotNullOrEmpty()][ValidateSet('expanded','collapsed','trigger')][string[]]$Workloads=@('expanded','collapsed','trigger'),
 [string]$OutputRoot, [switch]$CheckOnly)
$ErrorActionPreference='Stop'
$Counts=@($Counts | Select-Object -Unique)
$Workloads=@($Workloads | ForEach-Object { $_.ToLowerInvariant() } | Select-Object -Unique)
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskBuild=& (Join-Path $PSScriptRoot 'Get-M08Build.ps1')
$taskNative=& (Join-Path $PSScriptRoot 'Test-M08NativeEnvironment.ps1')
if ($CheckOnly) { [pscustomobject]@{Result='PREFLIGHT ONLY'; Build=$taskBuild; Native=$taskNative; Launched=$false}; return }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally before measuring; no forced termination.' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot=Join-Path $taskRepo ('out\verify\v3-04-m08-07\resources-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6)) }
$taskRoot=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if (Test-Path -LiteralPath $taskRoot) { throw 'Use a NEW result root; preserve prior evidence.' }
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Daily profile is forbidden.' }
$null=New-Item -ItemType Directory -Path $taskRoot
$taskNative | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'native-preflight.json') -Encoding utf8
if (-not $taskNative.Readable) {
 [pscustomobject]@{Result='BLOCKED - no valid samples collected'; Reason='Native input desktop unavailable; run after unlock on the interactive desktop'; DataRoot=$taskRoot}; return
}
if (-not ('DesktopUpMeasurementMath' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'MeasurementMath.cs') }
if (-not ('DesktopUpAutoMeasurementRules' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'M08MeasurementRules.cs') }
$taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
$taskElevated=([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$taskIdentity.Dispose()
$taskEnvironment=[ordered]@{ SchemaVersion=1; StartedUtc=[DateTimeOffset]::UtcNow; Build=$taskBuild;
 Os=(Get-CimInstance Win32_OperatingSystem | Select-Object Caption,BuildNumber);
 Cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);
 PhysicalMemoryMiB=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1MB;
 LogicalProcessors=[Environment]::ProcessorCount; Elevated=$taskElevated; PowerShell=$PSVersionTable.PSVersion.ToString();
 Contract=@{WarmupAfterReadyMs=8000; EndAfterReadyMs=18000; IntervalMs=500; MinimumSamples=19; Repeats=$Repeats;
  OffTrigger='programmatic manual collapse control at 10s, serial'; OnTrigger='real passive-pointer scheduler, leave delay 10s';
  SteadyDelaySeconds=300; PairOrder='OFF/ON reversed on even repeats'; Quantile='nearest-rank';
  Cpu='process delta CPU / monotonic observed wall; single core and machine normalized';
  Memory='WorkingSet64 and PrivateMemorySize64 (private committed bytes, not private working set)';
  NotMeasured='GPU, present FPS, physical mouse latency'; ProbeOverhead='same 250ms fixture timer in both arms; sparse markers only'}}
$taskEnvironment | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskRoot 'environment.json') -Encoding utf8
$taskReports=[Collections.Generic.List[object]]::new()
$taskDisplay=$null
foreach($taskCount in $Counts) { foreach($taskWorkload in $Workloads) { for($taskRepeat=1;$taskRepeat -le $Repeats;$taskRepeat++) {
 $taskOrder=if($taskRepeat % 2 -eq 1){@('off','on')}else{@('on','off')}
 foreach($taskSwitch in $taskOrder) {
  if(Test-Path -LiteralPath (Join-Path $taskRoot 'stop-after-current')){ Write-Output "STOPPED: partial results at $taskRoot"; return }
  $taskCurrentBuild=& (Join-Path $PSScriptRoot 'Get-M08Build.ps1')
  if($taskCurrentBuild.ManifestHash -ne $taskBuild.ManifestHash){throw 'Build binding changed during collection.'}
  if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Unexpected DesktopUp process; stop collection.'}
  $taskCase="n$taskCount-$taskSwitch-$taskWorkload-r$taskRepeat"
  $taskData=Join-Path $taskRoot $taskCase
  $taskBefore=& (Join-Path $PSScriptRoot 'Test-M08NativeEnvironment.ps1')
  if(-not $taskBefore.Readable){throw "Input desktop unavailable before $taskCase; partial evidence preserved at $taskRoot"}
  $taskPosture=if($taskWorkload -eq 'collapsed'){'collapsed'}else{'expanded'}
  & $taskBuild.Fixture $taskData $taskCount "--measure-$taskPosture" | Out-Null
  if($LASTEXITCODE -ne 0){throw 'Fixture failed.'}
  $taskMode="$taskSwitch-$taskWorkload"
  $taskArguments='--auto-measurement --measurement-mode=' + $taskMode + ' --exit-after-ms=40000 --animation-config="' + $taskBuild.AnimationConfig + '"'
  $taskProbe=Start-DesktopUpFixtureProcess -ExecutablePath $taskBuild.App -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
  $taskWatch=[Diagnostics.Stopwatch]::StartNew()
  $taskSamples=[Collections.Generic.List[object]]::new(); $taskNativeValid=$true
  while(-not $taskProbe.HasExited -and $taskWatch.ElapsedMilliseconds -lt 41000) {
   try {
    $taskProbe.Refresh()
    $taskInput=& (Join-Path $PSScriptRoot 'Test-M08NativeEnvironment.ps1')
    if(-not $taskInput.Readable){$taskNativeValid=$false}
    $taskSamples.Add([pscustomobject]@{Qpc=[Diagnostics.Stopwatch]::GetTimestamp(); CpuMs=$taskProbe.TotalProcessorTime.TotalMilliseconds;
     WorkingSetMiB=$taskProbe.WorkingSet64/1MB; PrivateCommitMiB=$taskProbe.PrivateMemorySize64/1MB;
     Handles=$taskProbe.HandleCount; Threads=$taskProbe.Threads.Count; NativeReadable=$taskInput.Readable})
   } catch {if(-not $taskProbe.HasExited){throw}}
   Start-Sleep -Milliseconds 500
  }
  if(-not $taskProbe.WaitForExit(5000)){throw "PID $($taskProbe.Id) did not exit normally. Do not kill it; inspect $taskData"}
  $taskProbe.Refresh()
  $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
  $taskMarkers=@($taskEvents | Where-Object eventName -eq 'auto_measurement.marker')
  $taskBegin=@($taskMarkers | Where-Object {$_.data.phase -eq 'begin'})
  $taskEnd=@($taskMarkers | Where-Object {$_.data.phase -eq 'end'})
  $taskDone=@($taskEvents | Where-Object {$_.eventName -eq 'auto_measurement.completed' -and $_.data.passed})
  $taskErrors=@($taskEvents | Where-Object level -in 'error','critical')
  $taskLedger=Get-Content -LiteralPath (Join-Path $taskData 'state\resource-ledger.json') -Raw | ConvertFrom-Json
  $taskPoints=@(); $taskSummary=$null; $taskRevisionDelta=-1
  if($taskBegin.Count -eq 1 -and $taskEnd.Count -eq 1) {
   $taskPoints=@($taskSamples | Where-Object {$_.Qpc -ge $taskBegin[0].data.qpc -and $_.Qpc -le $taskEnd[0].data.qpc})
   $taskRevisionDelta=$taskEnd[0].data.revision-$taskBegin[0].data.revision
   if($taskPoints.Count -ge 2) {
    $taskSpan=1000*($taskPoints[-1].Qpc-$taskPoints[0].Qpc)/[Diagnostics.Stopwatch]::Frequency
    $taskCpu=$taskPoints[-1].CpuMs-$taskPoints[0].CpuMs
    $taskSummary=[ordered]@{Samples=$taskPoints.Count; SpanMs=$taskSpan;
     CpuSecondsPer10s=$taskCpu/$taskSpan*10; CpuSingleCorePercent=[DesktopUpMeasurementMath]::CpuPercent($taskCpu,$taskSpan,1);
     CpuMachinePercent=[DesktopUpMeasurementMath]::CpuPercent($taskCpu,$taskSpan,[Environment]::ProcessorCount);
     WorkingSetMedianMiB=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.WorkingSetMiB,.5);
     WorkingSetP95MiB=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.WorkingSetMiB,.95);
     PrivateCommitMedianMiB=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.PrivateCommitMiB,.5);
     HandlesP95=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.Handles,.95);
     ThreadsP95=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.Threads,.95);
     ObservationSamples=$taskEnd[0].data.pointerSamples-$taskBegin[0].data.pointerSamples;
     AutoTimerAtBegin=$taskBegin[0].data.autoTimer; RevisionDelta=$taskRevisionDelta}
   }
  }
  $taskRequests=@($taskEvents | Where-Object eventName -eq 'auto_posture.request').Count
  $taskCommits=@($taskEvents | Where-Object {$_.eventName -eq 'auto_posture.completed' -and $_.data.outcome -eq 'Committed'}).Count
  $taskPostures=@($taskEvents | Where-Object eventName -eq 'spatial.posture_started').Count
  $taskSignatures=@($taskEvents | Where-Object eventName -eq 'display_topology.stable' | ForEach-Object {$_.data.Signature})
  if($null -eq $taskDisplay -and $taskSignatures.Count -eq 1){$taskDisplay=$taskSignatures[0]}
  $taskSameDisplay=$taskSignatures.Count -eq 1 -and $taskSignatures[0] -ceq $taskDisplay
  $taskValid=$taskProbe.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskErrors.Count -eq 0 -and
   $taskLedger.clean -and @($taskLedger.resources).Count -eq 0 -and $taskNativeValid -and $taskSameDisplay -and
   [DesktopUpAutoMeasurementRules]::CountsMatch($taskCount,$taskMode,$taskPoints.Count,$taskRequests,$taskCommits,$taskPostures,$taskRevisionDelta)
  $taskReport=[pscustomobject]@{Case=$taskCase; Valid=$taskValid; Count=$taskCount; Mode=$taskMode; Workload=$taskWorkload; Repeat=$taskRepeat;
   ProcessId=$taskProbe.Id; ExitCode=$taskProbe.ExitCode; Summary=$taskSummary; NativeValid=$taskNativeValid; DisplayStable=$taskSameDisplay;
   Requests=$taskRequests; AutoCommits=$taskCommits; Postures=$taskPostures; Errors=$taskErrors; ResourcesAfterExit=@($taskLedger.resources).Count;
   Markers=$taskMarkers.data; Samples=$taskSamples.ToArray(); AppHash=$taskBuild.AppHash;
   LogHash=(Get-FileHash -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl')).Hash}
  $taskReport | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath (Join-Path $taskData 'measurement.json') -Encoding utf8
  $taskReports.Add(($taskReport | Select-Object Case,Valid,Count,Mode,Workload,Repeat,ProcessId,Summary))
  $taskReports.ToArray() | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskRoot 'summary.json') -Encoding utf8
  Write-Output "$taskCase valid=$taskValid PID=$($taskProbe.Id)"
  $taskProbe.Dispose()
  if(-not $taskValid){throw "Invalid sample retained at $taskData; cannot publish baseline."}
 }
}}}
$taskPairs=@(foreach($taskRow in $taskReports | Where-Object {$_.Mode.StartsWith('on-')}) {
 $taskOff=@($taskReports | Where-Object {$_.Count -eq $taskRow.Count -and $_.Workload -eq $taskRow.Workload -and $_.Repeat -eq $taskRow.Repeat -and $_.Mode.StartsWith('off-')})
 if($taskOff.Count -ne 1){throw 'Missing paired control.'}
 [pscustomobject]@{Count=$taskRow.Count; Workload=$taskRow.Workload; Repeat=$taskRow.Repeat;
  CpuDeltaSecondsPer10s=$taskRow.Summary.CpuSecondsPer10s-$taskOff[0].Summary.CpuSecondsPer10s;
  WorkingSetDeltaMiB=$taskRow.Summary.WorkingSetMedianMiB-$taskOff[0].Summary.WorkingSetMedianMiB;
  PrivateCommitDeltaMiB=$taskRow.Summary.PrivateCommitMedianMiB-$taskOff[0].Summary.PrivateCommitMedianMiB}
})
$taskPairs | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot 'paired-deltas.json') -Encoding utf8
[pscustomobject]@{Result='COLLECTION COMPLETE - performance/visual judgment remains separate'; Samples=$taskReports.Count; Pairs=$taskPairs.Count; DataRoot=$taskRoot}
