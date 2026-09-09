#requires -Version 5.1
param(
 [Parameter(Mandatory)][string]$ExecutablePath,
 [Parameter(Mandatory)][string]$FixtureExecutablePath,
 [Parameter(Mandatory)][string]$OutputRoot,
 [ValidateSet(1,3,6)][int[]]$Counts = @(1,3,6),
 [ValidateRange(1,3)][int]$Repeats = 2,
 # Retained for older callers; elevation is now recorded, never a launch gate.
 [switch]$AllowElevatedReference
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskFixture = (Resolve-Path -LiteralPath $FixtureExecutablePath).Path
$taskRoot = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if (Test-Path -LiteralPath $taskRoot) { throw 'Measurement output requires a NEW directory; prior evidence is never overwritten.' }
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Normal profile is forbidden.' }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Close DesktopUp normally first.' }
if (-not ('DesktopUpMeasurementMath' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'MeasurementMath.cs') }
$taskIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$taskElevated = ([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$taskIdentity.Dispose()
$null = New-Item -ItemType Directory -Path $taskRoot
$taskCpu = @(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors)
$taskSystem = Get-CimInstance Win32_ComputerSystem
$taskOs = Get-CimInstance Win32_OperatingSystem
$taskEnvironment = [ordered]@{
 SchemaVersion=1; StartedUtc=[DateTimeOffset]::UtcNow; Os=$taskOs.Caption; OsBuild=$taskOs.BuildNumber;
 Cpu=$taskCpu; LogicalProcessors=[Environment]::ProcessorCount; PhysicalMemoryMiB=$taskSystem.TotalPhysicalMemory/1MB;
 Elevated=$taskElevated; EvidenceScope='current-environment'; EligibleForStandardUserBaseline=(-not $taskElevated); CollectorRuntime=[Environment]::Version.ToString(); PowerShell=$PSVersionTable.PSVersion.ToString();
 Build=(Get-FileHash -LiteralPath (@('DesktopUp.exe','DesktopUp.dll','DesktopUp.Domain.dll','DesktopUp.Application.dll','DesktopUp.Windows.dll','DesktopUp.Infrastructure.dll') | ForEach-Object { Join-Path (Split-Path $taskExe) $_ }) | Select-Object Path,Hash);
 Contract=@{ WarmupFromLaunchMs=8000; WindowEndMs=18000; IntervalMs=500; MinimumSamples=19; Repeats=$Repeats; Quantile='nearest-rank';
   Cpu='delta TotalProcessorTime / measured monotonic wall time; single-core and total-machine both reported';
   Memory='WorkingSet64 resident pages; PrivateMemorySize64 private committed bytes, not private working set';
   Gpu='not measured'; PresentedFps='not measured; native submissions are not presented frames'; PhysicalInputLatency='not measured' }
}
$taskEnvironment | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskRoot 'environment.json') -Encoding utf8
$taskResults = [Collections.Generic.List[object]]::new()
foreach ($taskCount in $Counts) { foreach ($taskPosture in @('expanded','collapsed','hidden')) { foreach ($taskCenter in @($false,$true)) { for ($taskRepeat=1; $taskRepeat -le $Repeats; $taskRepeat++) {
 if (Test-Path -LiteralPath (Join-Path $taskRoot 'stop-after-current')) { Write-Output 'STOPPED after current case; partial evidence retained.'; return }
 if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Unexpected live DesktopUp before next sample.' }
 $taskCase = "n$taskCount-$taskPosture-center$([int]$taskCenter)-r$taskRepeat"
 $taskData = Join-Path $taskRoot $taskCase
 & $taskFixture $taskData $taskCount "--measure-$taskPosture" | Out-Null
 if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $taskCase" }
 $taskArguments = @('--exit-after-ms=20000')
 if ($taskCenter) { $taskArguments += '--show-control-center' }
 $taskWatch = [Diagnostics.Stopwatch]::StartNew()
 $taskLaunchUtc = [DateTimeOffset]::UtcNow
 $taskProbe = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments ($taskArguments -join ' ')
 $taskSamples = [Collections.Generic.List[object]]::new()
 while (-not $taskProbe.HasExited -and $taskWatch.ElapsedMilliseconds -lt 19000) {
  try {
   $taskProbe.Refresh()
   $taskSamples.Add([pscustomobject]@{ ElapsedMs=$taskWatch.Elapsed.TotalMilliseconds; CpuMs=$taskProbe.TotalProcessorTime.TotalMilliseconds;
    WorkingSetMiB=$taskProbe.WorkingSet64/1MB; PrivateCommitMiB=$taskProbe.PrivateMemorySize64/1MB;
    Handles=$taskProbe.HandleCount; Threads=$taskProbe.Threads.Count })
  } catch { if (-not $taskProbe.HasExited) { throw } }
  Start-Sleep -Milliseconds 500
 }
 if (-not $taskProbe.WaitForExit(12000)) { throw "No forced termination: PID $($taskProbe.Id) did not exit; inspect $taskData" }
 $taskProbe.Refresh()
 $taskEvents = @(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
 $taskLedger = Get-Content -LiteralPath (Join-Path $taskData 'state\resource-ledger.json') -Raw | ConvertFrom-Json
 $taskReady = @($taskEvents | Where-Object eventName -eq 'poster_session.ready')
 $taskExpectedVisible = if ($taskPosture -eq 'hidden') { 0 } else { $taskCount }
 $taskEnvironmentReady = @($taskEvents | Where-Object eventName -eq 'desktop_environment.recovery_committed')
 $taskCenters = @($taskEvents | Where-Object eventName -eq 'control_center.created')
 $taskTrays = @($taskEvents | Where-Object eventName -eq 'tray.created')
 $taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
 $taskWarnings = @($taskEvents | Where-Object level -eq 'warning' | Select-Object eventName,data)
 $taskPoints = @($taskSamples | Where-Object { $_.ElapsedMs -ge 8000 -and $_.ElapsedMs -le 18000 })
 $taskRelevantReady = @($taskReady) + @($taskCenters) + @($taskEnvironmentReady)
 $taskReadyMs = if ($taskRelevantReady.Count -gt 0) { ($taskRelevantReady | ForEach-Object { ([DateTimeOffset]$_.timestampUtc - $taskLaunchUtc).TotalMilliseconds } | Measure-Object -Maximum).Maximum } else { $null }
 $taskShown = @($taskEvents | Where-Object eventName -eq 'poster.shown')
 $taskOverlaps = 0
 for ($taskA=0; $taskA -lt $taskShown.Count; $taskA++) { for ($taskB=$taskA+1; $taskB -lt $taskShown.Count; $taskB++) {
  $a=$taskShown[$taskA].data.bounds; $b=$taskShown[$taskB].data.bounds
  if ($a.X -lt $b.X+$b.Width -and $a.X+$a.Width -gt $b.X -and $a.Y -lt $b.Y+$b.Height -and $a.Y+$a.Height -gt $b.Y) { $taskOverlaps++ }
 } }
 $taskValid = $taskProbe.ExitCode -eq 0 -and $taskErrors.Count -eq 0 -and $taskLedger.clean -and @($taskLedger.resources).Count -eq 0 -and
  $taskReady.Count -eq $taskExpectedVisible -and $taskShown.Count -eq $taskExpectedVisible -and $taskCenters.Count -eq [int]$taskCenter -and
  $taskTrays.Count -eq 1 -and $taskEnvironmentReady.Count -eq 1 -and $null -ne $taskReadyMs -and $taskReadyMs -lt 8000 -and $taskPoints.Count -ge 19 -and $taskOverlaps -eq 0
 $taskSummary = $null
 if ($taskPoints.Count -ge 2) {
  $taskSpan = $taskPoints[-1].ElapsedMs-$taskPoints[0].ElapsedMs
  $taskCpuDelta = $taskPoints[-1].CpuMs-$taskPoints[0].CpuMs
  $taskSummary = [ordered]@{ SampleCount=$taskPoints.Count; WindowSpanMs=$taskSpan; CpuDeltaSeconds=$taskCpuDelta/1000;
   CpuSecondsPer10s=$taskCpuDelta/$taskSpan*10;
   CpuSingleCorePercent=[DesktopUpMeasurementMath]::CpuPercent($taskCpuDelta,$taskSpan,1);
   CpuMachinePercent=[DesktopUpMeasurementMath]::CpuPercent($taskCpuDelta,$taskSpan,[Environment]::ProcessorCount);
   WorkingSetMedianMiB=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.WorkingSetMiB,.5);
   WorkingSetP95MiB=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.WorkingSetMiB,.95);
   WorkingSetMaxMiB=($taskPoints.WorkingSetMiB | Measure-Object -Maximum).Maximum;
   PrivateCommitMedianMiB=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.PrivateCommitMiB,.5);
   HandlesMedian=[DesktopUpMeasurementMath]::Quantile([double[]]$taskPoints.Handles,.5);
   HandlesMax=($taskPoints.Handles | Measure-Object -Maximum).Maximum; ThreadsMax=($taskPoints.Threads | Measure-Object -Maximum).Maximum;
   LaunchToAllSessionsAndEnvironmentReadyMs=$taskReadyMs }
 }
 $taskReport = [ordered]@{ Case=$taskCase; Valid=$taskValid; Elevated=$taskElevated; EvidenceScope='current-environment'; EligibleForStandardUserBaseline=(-not $taskElevated); Count=$taskCount; Posture=$taskPosture; CenterOpen=$taskCenter; Repeat=$taskRepeat;
  ProcessId=$taskProbe.Id; ExitCode=$taskProbe.ExitCode; Summary=$taskSummary; WarningEvents=$taskWarnings; Errors=$taskErrors;
  Overlaps=$taskOverlaps; ResourcesAfterExit=@($taskLedger.resources).Count; Samples=$taskSamples.ToArray();
  Display=@($taskEvents | Where-Object eventName -eq 'display_topology.stable' | ForEach-Object data);
  PosterBounds=@($taskShown.data.bounds); LogHash=(Get-FileHash -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl')).Hash }
 $taskReport | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $taskData 'measurement.json') -Encoding utf8
 $taskResults.Add([pscustomobject]@{ Case=$taskCase; Valid=$taskValid; Count=$taskCount; Posture=$taskPosture; CenterOpen=$taskCenter; Repeat=$taskRepeat;
  ProcessId=$taskProbe.Id; Summary=$taskSummary; WarningCount=$taskWarnings.Count; Evidence=(Join-Path $taskData 'measurement.json') })
 $taskResults.ToArray() | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $taskRoot 'summary.json') -Encoding utf8
 Write-Output "$taskCase valid=$taskValid PID=$($taskProbe.Id) WSmedian=$([Math]::Round($taskSummary.WorkingSetMedianMiB,2)) MiB CPU10s=$([Math]::Round($taskSummary.CpuSecondsPer10s,4)) s"
 $taskProbe.Dispose()
 if (-not $taskValid) { throw "Invalid measurement retained: $taskCase. Do not publish this as a valid baseline." }
} } } }
Write-Output "COMPLETE: $($taskResults.Count) measurements; $taskRoot"
