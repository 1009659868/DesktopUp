[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$QtRoot,
    [ValidateRange(1, 10)]
    [int]$RepeatCount = 3,
    [ValidateRange(1, 30)]
    [int]$WarmupSeconds = 2,
    [ValidateRange(5, 60)]
    [int]$SampleCount = 10,
    [ValidateRange(1, 5)]
    [int]$SampleIntervalSeconds = 1,
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode {
    param([Parameter(Mandatory)][string]$Operation)
    if ($LASTEXITCODE -notin @(0, $null)) {
        throw "$Operation failed with exit code $LASTEXITCODE."
    }
}

function Wait-ForReady {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [string]$MarkerPath,
        [string]$LogPath,
        [string]$LogPattern,
        [int]$TimeoutSeconds = 20
    )
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process.HasExited) {
            throw "Baseline process $($Process.Id) exited before readiness."
        }
        if ($MarkerPath -and (Test-Path -LiteralPath $MarkerPath -PathType Leaf)) {
            return
        }
        if ($LogPattern -and (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
            $raw = Get-Content -LiteralPath $LogPath -Raw -ErrorAction SilentlyContinue
            if ($raw -match $LogPattern) { return }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for baseline process $($Process.Id) readiness."
}

function Get-CounterValue {
    param(
        [Parameter(Mandatory)]$Samples,
        [Parameter(Mandatory)][string]$InstanceName,
        [Parameter(Mandatory)][string]$CounterSuffix
    )
    $sample = $Samples | Where-Object {
        $_.InstanceName -eq $InstanceName -and
        $_.Path.ToLowerInvariant().EndsWith($CounterSuffix)
    } | Select-Object -First 1
    if ($null -eq $sample) { return $null }
    return [double]$sample.CookedValue
}

function Get-MetricSummary {
    param([Parameter(Mandatory)][double[]]$Values)
    if ($Values.Count -eq 0) { return $null }
    $measure = $Values | Measure-Object -Minimum -Maximum -Average
    return [ordered]@{
        min = [Math]::Round([double]$measure.Minimum, 3)
        avg = [Math]::Round([double]$measure.Average, 3)
        max = [Math]::Round([double]$measure.Maximum, 3)
    }
}

function Get-Median {
    param([Parameter(Mandatory)][double[]]$Values)
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) {
        return [double]$sorted[$middle]
    }
    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2.0
}

function Read-StaticFrameResult {
    param(
        [Parameter(Mandatory)][string]$RoleId,
        [Parameter(Mandatory)][string]$LogPath
    )
    if ($RoleId -eq 'desktop_host') {
        return [ordered]@{ value = $null; status = 'NOT_INSTRUMENTED_NO_FRAME_SCHEDULER' }
    }
    $events = @(
        Get-Content -LiteralPath $LogPath -ErrorAction Stop |
            Where-Object { $_.Trim().Length -gt 0 } |
            ForEach-Object { $_ | ConvertFrom-Json }
    )
    if ($RoleId -eq 'current_single_poster') {
        $matrix = $events | Where-Object {
            $_.event -eq 'r2_fixture_matrix_completed' -and $_.result -eq 'PASS'
        } | Select-Object -Last 1
        if ($null -eq $matrix) {
            return [ordered]@{ value = $null; status = 'MATRIX_MARKER_MISSING' }
        }
        $count = @($events | Where-Object {
            $_.event -eq 'frame_presented' -and
            [double]$_.monotonic_ms -gt [double]$matrix.monotonic_ms
        }).Count
        return [ordered]@{ value = $count; status = 'MEASURED_FROM_EVENT_LOG' }
    }
    $finished = $events | Where-Object {
        $_.event -eq 'baseline_finished'
    } | Select-Object -Last 1
    if ($null -eq $finished) {
        return [ordered]@{ value = $null; status = 'FINISH_EVENT_MISSING' }
    }
    return [ordered]@{
        value = [int]$finished.static_frame_count
        status = 'MEASURED_FROM_EVENT_LOG'
    }
}

function Invoke-RoleRun {
    param(
        [Parameter(Mandatory)][string]$RoleId,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$RoleRunRoot,
        [string]$ReadyMarker,
        [string]$ReadyLogPattern
    )
    New-Item -ItemType Directory -Path $RoleRunRoot -Force | Out-Null
    $stdout = Join-Path $RoleRunRoot 'stdout.log'
    $stderr = Join-Path $RoleRunRoot 'stderr.log'
    $eventLog = Join-Path $RoleRunRoot 'events.jsonl'

    $process = $null
    try {
        $process = Start-Process -FilePath $Executable -ArgumentList $Arguments `
            -WorkingDirectory (Split-Path -Parent $Executable) `
            -WindowStyle Hidden -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr -PassThru
        Wait-ForReady -Process $process -MarkerPath $ReadyMarker `
            -LogPath $eventLog -LogPattern $ReadyLogPattern
        Start-Sleep -Seconds $WarmupSeconds
        $process.Refresh()
        $cpuStartMs = $process.TotalProcessorTime.TotalMilliseconds

        $counterPaths = @(
            '\Process(*)\ID Process',
            '\Process(*)\Working Set',
            '\Process(*)\Working Set - Private',
            '\Process(*)\Private Bytes',
            '\GPU Process Memory(*)\Dedicated Usage',
            '\GPU Process Memory(*)\Shared Usage',
            '\GPU Process Memory(*)\Total Committed'
        )
        $sets = @(Get-Counter -Counter $counterPaths `
            -SampleInterval $SampleIntervalSeconds -MaxSamples $SampleCount -ErrorAction SilentlyContinue)
        if ($sets.Count -ne $SampleCount) {
            throw "Expected $SampleCount counter sets, received $($sets.Count)."
        }

        $rows = [System.Collections.Generic.List[object]]::new()
        foreach ($set in $sets) {
            $idSample = $set.CounterSamples | Where-Object {
                $_.Path.ToLowerInvariant().EndsWith('\id process') -and
                [int][Math]::Round([double]$_.CookedValue) -eq $process.Id
            } | Select-Object -First 1
            if ($null -eq $idSample) {
                throw "Process performance-counter instance for PID $($process.Id) disappeared."
            }
            $instance = $idSample.InstanceName
            $workingSet = Get-CounterValue $set.CounterSamples $instance '\working set'
            $privateWorkingSet = Get-CounterValue $set.CounterSamples $instance '\working set - private'
            $privateBytes = Get-CounterValue $set.CounterSamples $instance '\private bytes'

            $gpuSamples = @($set.CounterSamples | Where-Object {
                $_.InstanceName.StartsWith("pid_$($process.Id)_", [StringComparison]::OrdinalIgnoreCase)
            })
            $gpuDedicatedSamples = @($gpuSamples | Where-Object {
                $_.Path.ToLowerInvariant().EndsWith('\dedicated usage')
            })
            $gpuSharedSamples = @($gpuSamples | Where-Object {
                $_.Path.ToLowerInvariant().EndsWith('\shared usage')
            })
            $gpuCommittedSamples = @($gpuSamples | Where-Object {
                $_.Path.ToLowerInvariant().EndsWith('\total committed')
            })
            $gpuDedicated = if ($gpuDedicatedSamples.Count -gt 0) {
                ($gpuDedicatedSamples | Measure-Object -Property CookedValue -Sum).Sum
            } else { 0 }
            $gpuShared = if ($gpuSharedSamples.Count -gt 0) {
                ($gpuSharedSamples | Measure-Object -Property CookedValue -Sum).Sum
            } else { 0 }
            $gpuCommitted = if ($gpuCommittedSamples.Count -gt 0) {
                ($gpuCommittedSamples | Measure-Object -Property CookedValue -Sum).Sum
            } else { 0 }

            $rows.Add([ordered]@{
                timestamp = $set.Timestamp.ToUniversalTime().ToString('o')
                workingSetMiB = [Math]::Round($workingSet / 1MB, 3)
                privateWorkingSetMiB = [Math]::Round($privateWorkingSet / 1MB, 3)
                commitMiB = [Math]::Round($privateBytes / 1MB, 3)
                gpuDedicatedMiB = [Math]::Round([double]$gpuDedicated / 1MB, 3)
                gpuSharedMiB = [Math]::Round([double]$gpuShared / 1MB, 3)
                gpuCommittedMiB = [Math]::Round([double]$gpuCommitted / 1MB, 3)
            })
        }
        $rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath `
            (Join-Path $RoleRunRoot 'counter-samples.json') -Encoding UTF8

        $process.Refresh()
        $cpuEndMs = $process.TotalProcessorTime.TotalMilliseconds
        if (!$process.WaitForExit(15000)) {
            throw "Baseline process $($process.Id) did not exit within its contract duration."
        }
        if ($process.ExitCode -ne 0) {
            throw "Baseline process $($process.Id) exited with code $($process.ExitCode)."
        }
        $staticFrames = Read-StaticFrameResult -RoleId $RoleId -LogPath $eventLog
        $sampleDurationSeconds = $SampleCount * $SampleIntervalSeconds
        $cpuDeltaMs = [Math]::Max(0.0, $cpuEndMs - $cpuStartMs)

        $summary = [ordered]@{
            schema = 'desktopup.qts-01-01.role-run.v1'
            roleId = $RoleId
            executable = [IO.Path]::GetRelativePath($repoRoot, $Executable)
            executableSha256 = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
            pid = $process.Id
            warmupSeconds = $WarmupSeconds
            sampleCount = $SampleCount
            sampleIntervalSeconds = $SampleIntervalSeconds
            sampleDurationSeconds = $sampleDurationSeconds
            workingSetMiB = Get-MetricSummary ([double[]]@($rows.workingSetMiB))
            privateWorkingSetMiB = Get-MetricSummary ([double[]]@($rows.privateWorkingSetMiB))
            commitMiB = Get-MetricSummary ([double[]]@($rows.commitMiB))
            gpuDedicatedMiB = Get-MetricSummary ([double[]]@($rows.gpuDedicatedMiB))
            gpuSharedMiB = Get-MetricSummary ([double[]]@($rows.gpuSharedMiB))
            gpuCommittedMiB = Get-MetricSummary ([double[]]@($rows.gpuCommittedMiB))
            cpuTimeDeltaMs = [Math]::Round($cpuDeltaMs, 3)
            singleCoreCpuPercent = [Math]::Round(
                100.0 * $cpuDeltaMs / ($sampleDurationSeconds * 1000.0), 3)
            staticFrames = $staticFrames.value
            staticFrameStatus = $staticFrames.status
            exitCode = $process.ExitCode
        }
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath `
            (Join-Path $RoleRunRoot 'run-summary.json') -Encoding UTF8
        return $summary
    }
    finally {
        if ($null -ne $process -and !$process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(5000) | Out-Null
        }
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..\..'))
$nativeSource = Join-Path $repoRoot 'prototype\desktop-host-win32\ts-h01-b'
$posterSource = Join-Path $repoRoot 'prototype\renderer-adapters\ts-c01-qt-quick'
$outRoot = Join-Path $scriptRoot 'out'
$qtBuild = Join-Path $outRoot 'build-qt'
$nativeBuild = Join-Path $outRoot 'build-native'
$posterBuild = Join-Path $outRoot 'build-current-poster'
$qtBin = Join-Path $QtRoot 'bin'
$cmakePrefix = $QtRoot.Replace('\', '/')

if (!(Test-Path -LiteralPath (Join-Path $qtBin 'Qt6Core.dll') -PathType Leaf)) {
    throw "Qt runtime not found under $QtRoot."
}
$cmake = (Get-Command cmake -ErrorAction Stop).Source
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

& $cmake -S $scriptRoot -B $qtBuild -G 'Visual Studio 18 2026' -A x64 `
    "-DCMAKE_PREFIX_PATH=$cmakePrefix"
Assert-LastExitCode 'Configure QTS-01-01 Qt baselines'
& $cmake --build $qtBuild --config Release
Assert-LastExitCode 'Build QTS-01-01 Qt baselines'

& $cmake -S $nativeSource -B $nativeBuild -G 'Visual Studio 18 2026' -A x64
Assert-LastExitCode 'Configure native DesktopHost baseline'
& $cmake --build $nativeBuild --config Release
Assert-LastExitCode 'Build native DesktopHost baseline'

& $cmake -S $posterSource -B $posterBuild -G 'Visual Studio 18 2026' -A x64 `
    "-DCMAKE_PREFIX_PATH=$cmakePrefix"
Assert-LastExitCode 'Configure current single-poster baseline'
& $cmake --build $posterBuild --config Release --target ts-c01-qt-r2
Assert-LastExitCode 'Build current single-poster baseline'

$executables = [ordered]@{
    desktop_host = Join-Path $nativeBuild 'Release\desktop-host-layer-probe.exe'
    qt_gui_no_quick = Join-Path $qtBuild 'Release\qts-01-01-qt-gui.exe'
    minimal_qt_quick = Join-Path $qtBuild 'Release\qts-01-01-minimal-qt-quick.exe'
    offscreen_renderer = Join-Path $qtBuild 'Release\qts-01-01-offscreen-renderer.exe'
    current_single_poster = Join-Path $posterBuild 'Release\ts-c01-qt-r2.exe'
}
foreach ($entry in $executables.GetEnumerator()) {
    if (!(Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "Missing $($entry.Key) executable: $($entry.Value)"
    }
}

$env:PATH = "$qtBin;$env:PATH"
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot `
        "Docs\A_Project\30_Architecture\Evidence\QTS-01-01\$timestamp"
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)

$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpu = @(Get-CimInstance Win32_VideoController | ForEach-Object {
    [ordered]@{ name = $_.Name; driverVersion = $_.DriverVersion }
})
$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$gitStatus = @(& git -C $repoRoot status --short)
$machine = [ordered]@{
    schema = 'desktopup.qts-01-01.machine.v1'
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    computerName = 'redacted-local-host'
    osCaption = $os.Caption
    osVersion = $os.Version
    osBuild = $os.BuildNumber
    cpu = $cpu.Name
    logicalProcessors = $cpu.NumberOfLogicalProcessors
    physicalMemoryMiB = [Math]::Round([double]$os.TotalVisibleMemorySize / 1KB, 0)
    gpu = $gpu
    qtRoot = '<external-qt-root>'
    qtVersion = '6.8.3'
    generator = 'Visual Studio 18 2026 / v143 / x64 / Release'
    gitCommit = $gitCommit
    gitDirty = $gitStatus.Count -gt 0
    gitStatus = $gitStatus
    warmupSeconds = $WarmupSeconds
    sampleCount = $SampleCount
    sampleIntervalSeconds = $SampleIntervalSeconds
    repeatCount = $RepeatCount
    workingSetTrimCalled = $false
    processCounterSource = 'Windows Process performance counters'
    gpuCounterSource = 'Windows GPU Process Memory performance counters'
}
$machine | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath `
    (Join-Path $OutputRoot 'machine.json') -Encoding UTF8

$allRuns = [System.Collections.Generic.List[object]]::new()
$roleOrder = @(
    'desktop_host',
    'qt_gui_no_quick',
    'minimal_qt_quick',
    'offscreen_renderer',
    'current_single_poster'
)
$durationMs = 1000 * (
    $WarmupSeconds + ($SampleCount * $SampleIntervalSeconds) + 3)

foreach ($roleId in $roleOrder) {
    for ($repeat = 1; $repeat -le $RepeatCount; ++$repeat) {
        $roleRunRoot = Join-Path $OutputRoot ("{0}\run-{1:D2}" -f $roleId, $repeat)
        New-Item -ItemType Directory -Path $roleRunRoot -Force | Out-Null
        $eventLog = Join-Path $roleRunRoot 'events.jsonl'
        $readyMarker = Join-Path $roleRunRoot 'ready.txt'
        $arguments = @()
        $marker = $readyMarker
        $pattern = ''
        if ($roleId -eq 'desktop_host') {
            $arguments = @('--run-host-test', '--duration-ms', [string]$durationMs,
                '--log', $eventLog)
            $marker = ''
            $pattern = '"event":"host_created"'
        } elseif ($roleId -eq 'current_single_poster') {
            $arguments = @('--run-r2-fixture', '--r3-diagnostics',
                '--r3-ready-marker', $readyMarker, '--duration-ms',
                [string]$durationMs, '--log', $eventLog)
        } else {
            $arguments = @('--duration-ms', [string]$durationMs,
                '--ready-marker', $readyMarker, '--log', $eventLog)
        }
        Write-Host ("[QTS-01-01] {0} repetition {1}/{2}" -f `
            $roleId, $repeat, $RepeatCount)
        $run = Invoke-RoleRun -RoleId $roleId `
            -Executable $executables[$roleId] -Arguments $arguments `
            -RoleRunRoot $roleRunRoot -ReadyMarker $marker `
            -ReadyLogPattern $pattern
        $run.repeat = $repeat
        $allRuns.Add($run)
    }
}

$roleSummaries = [System.Collections.Generic.List[object]]::new()
foreach ($roleId in $roleOrder) {
    $runs = @($allRuns | Where-Object { $_.roleId -eq $roleId })
    $staticValues = @($runs | Where-Object { $null -ne $_.staticFrames } |
        ForEach-Object { [double]$_.staticFrames })
    $summary = [ordered]@{
        roleId = $roleId
        repeatCount = $runs.Count
        workingSetMiBMedianAverage = [Math]::Round(
            (Get-Median ([double[]]@($runs.workingSetMiB.avg))), 3)
        workingSetMiBEnvelopeMax = [Math]::Round(
            [double](($runs.workingSetMiB.max | Measure-Object -Maximum).Maximum), 3)
        privateWorkingSetMiBMedianAverage = [Math]::Round(
            (Get-Median ([double[]]@($runs.privateWorkingSetMiB.avg))), 3)
        privateWorkingSetMiBEnvelopeMax = [Math]::Round(
            [double](($runs.privateWorkingSetMiB.max | Measure-Object -Maximum).Maximum), 3)
        commitMiBMedianAverage = [Math]::Round(
            (Get-Median ([double[]]@($runs.commitMiB.avg))), 3)
        commitMiBEnvelopeMax = [Math]::Round(
            [double](($runs.commitMiB.max | Measure-Object -Maximum).Maximum), 3)
        gpuCommittedMiBMedianAverage = [Math]::Round(
            (Get-Median ([double[]]@($runs.gpuCommittedMiB.avg))), 3)
        gpuCommittedMiBEnvelopeMax = [Math]::Round(
            [double](($runs.gpuCommittedMiB.max | Measure-Object -Maximum).Maximum), 3)
        cpuTimeDeltaMsMedian = [Math]::Round(
            (Get-Median ([double[]]@($runs.cpuTimeDeltaMs))), 3)
        singleCoreCpuPercentMedian = [Math]::Round(
            (Get-Median ([double[]]@($runs.singleCoreCpuPercent))), 3)
        staticFramesEnvelopeMax = if ($staticValues.Count -gt 0) {
            [int](($staticValues | Measure-Object -Maximum).Maximum)
        } else { $null }
        staticFrameStatus = @($runs.staticFrameStatus | Select-Object -Unique)
    }
    $roleSummaries.Add($summary)
}

$baseline = [ordered]@{
    schema = 'desktopup.qts-01-01.baseline.v1'
    contract = 'QTS_01_01_MEMORY_MEASUREMENT_CONTRACT_V1'
    result = 'BASELINE_CAPTURED'
    budgetDecision = 'NOT_EVALUATED_BY_QTS-01-01'
    machine = $machine
    roles = $roleSummaries
    runs = $allRuns
}
$baseline | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath `
    (Join-Path $OutputRoot 'baseline.json') -Encoding UTF8
$roleSummaries | ForEach-Object { [pscustomobject]$_ } |
    Export-Csv -LiteralPath (Join-Path $OutputRoot 'baseline.csv') `
        -NoTypeInformation -Encoding UTF8

Write-Host "QTS-01-01 baseline captured: $OutputRoot"
