[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$QtInstallerRoot,

    [Parameter()]
    [ValidateRange(2000, 10000)]
    [int]$StaticSampleDurationMs = 10000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedQtVersion = '6.8.3'
$expectedWindowsSdk = '10.0.26100.0'
$expectedGenerator = 'Visual Studio 18 2026'
$expectedToolset = 'v143,version=14.44,host=x64'
$sourceDateEpoch = '1704067200'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputRoot = Join-Path $scriptRoot 'out'
$e5Root = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..\desktop-host-win32\ts-h01-e5')).Path
$validatorSelfTest = Join-Path $e5Root 'test-validator.ps1'
$validator = Join-Path $e5Root 'validate-desktop-host-events.ps1'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $outputRoot "r3-run-$runId"
$summaryPath = Join-Path $outputRoot 'r3-preflight.json'

function Assert-LastExitCode {
    param([Parameter(Mandatory)][string]$Operation)
    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE."
    }
}

function Resolve-QtPrefix {
    param([Parameter(Mandatory)][string]$InstallerRoot)
    $candidates = @(
        (Join-Path $InstallerRoot '6.8.3\msvc2022_64'),
        (Join-Path $InstallerRoot 'msvc2022_64'),
        $InstallerRoot
    )
    foreach ($candidate in $candidates) {
        $qmake = Join-Path $candidate 'bin\qmake.exe'
        $qtConfig = Join-Path $candidate 'lib\cmake\Qt6\Qt6Config.cmake'
        if ((Test-Path -LiteralPath $qmake -PathType Leaf) -and
            (Test-Path -LiteralPath $qtConfig -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw "Qt 6.8.3 MSVC 2022 x64 prefix is incomplete under '$InstallerRoot'."
}

function Get-RequiredTool {
    param(
        [Parameter(Mandatory)][string]$Base,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Label
    )
    $candidate = Join-Path $Base $RelativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "$Label was not found at '$candidate'."
    }
    return (Resolve-Path -LiteralPath $candidate).Path
}

function Invoke-CleanBuild {
    param(
        [Parameter(Mandatory)][string]$BuildRoot,
        [Parameter(Mandatory)][string]$QtPrefix,
        [Parameter(Mandatory)][string]$CMakePath
    )
    New-Item -ItemType Directory -Path $BuildRoot | Out-Null
    & $CMakePath -E env "SOURCE_DATE_EPOCH=$sourceDateEpoch" $CMakePath -S $scriptRoot -B $BuildRoot -G $expectedGenerator -A x64 -T $expectedToolset "-DCMAKE_SYSTEM_VERSION=$expectedWindowsSdk" "-DCMAKE_PREFIX_PATH=$QtPrefix" | Out-Host
    Assert-LastExitCode -Operation "CMake configure '$BuildRoot'"
    & $CMakePath -E env "SOURCE_DATE_EPOCH=$sourceDateEpoch" $CMakePath --build $BuildRoot --config Release --target ts-c01-qt-r2 --parallel 8 | Out-Host
    Assert-LastExitCode -Operation "CMake Release build '$BuildRoot'"

    $cachePath = Join-Path $BuildRoot 'CMakeCache.txt'
    $cacheText = Get-Content -LiteralPath $cachePath -Raw -Encoding UTF8
    if ($cacheText -notmatch [regex]::Escape('CMAKE_GENERATOR_TOOLSET:INTERNAL=v143,version=14.44,host=x64')) {
        throw "Build '$BuildRoot' did not use the frozen v143/14.44/host=x64 toolset."
    }
    $sdkPattern = "(?m)^CMAKE_SYSTEM_VERSION:[^=]+=$([regex]::Escape($expectedWindowsSdk))\r?$"
    if ($cacheText -notmatch $sdkPattern) {
        throw "Build '$BuildRoot' did not use Windows SDK $expectedWindowsSdk."
    }
    $executable = Join-Path $BuildRoot 'Release\ts-c01-qt-r2.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Release executable was not produced at '$executable'."
    }
    return (Resolve-Path -LiteralPath $executable).Path
}

function Remove-VerifiedCleanBuildRoot {
    param(
        [Parameter(Mandatory)][string]$BuildRoot,
        [Parameter(Mandatory)][string]$ExpectedParent
    )
    $expectedParentFull = [IO.Path]::GetFullPath($ExpectedParent).TrimEnd('\')
    $buildFull = [IO.Path]::GetFullPath($BuildRoot).TrimEnd('\')
    $isExpectedParent = [StringComparer]::OrdinalIgnoreCase.Equals(
        [IO.Path]::GetDirectoryName($buildFull),
        $expectedParentFull
    )
    if (-not $isExpectedParent -or [IO.Path]::GetFileName($buildFull) -ne 'build-canonical') {
        throw "Refusing to remove unexpected clean-build path '$buildFull'."
    }
    if (-not (Test-Path -LiteralPath $buildFull -PathType Container)) {
        throw "Clean-build path does not exist: '$buildFull'."
    }
    $resolved = (Resolve-Path -LiteralPath $buildFull).Path.TrimEnd('\')
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($resolved, $buildFull)) {
        throw "Resolved clean-build path '$resolved' differs from expected '$buildFull'."
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if (Test-Path -LiteralPath $resolved) {
        throw "Clean-build path remains after removal: '$resolved'."
    }
}

function Read-Events {
    param([Parameter(Mandatory)][string]$Path)
    return @(Get-Content -LiteralPath $Path -Encoding UTF8 | ForEach-Object {
        $_ | ConvertFrom-Json -Depth 24
    })
}

function Assert-ZeroCleanup {
    param(
        [Parameter(Mandatory)][object[]]$Events,
        [Parameter(Mandatory)][string]$Label,
        [int]$ExpectedPresentationReleaseCount = 1
    )
    $cleanup = @($Events | Where-Object {
        $_.event -eq 'cleanup_completed' -and
        $_.resource_counts.host -eq 0 -and
        $_.resource_counts.adapter -eq 0 -and
        $_.resource_counts.quarantine -eq 0
    })
    $visual = @($Events | Where-Object {
        $_.event -eq 'host_layered_visual_released' -and
        $_.live_visual_count -eq 0 -and
        $_.unexpected_visual_pointer_count -eq 0
    })
    $islands = @($Events | Where-Object {
        $_.event -eq 'input_islands_released' -and
        $_.live_island_count -eq 0
    })
    if ($cleanup.Count -ne 1 -or
        $visual.Count -ne $ExpectedPresentationReleaseCount -or
        $islands.Count -ne $ExpectedPresentationReleaseCount) {
        throw "$Label did not report the expected zero-resource cleanup/release counts."
    }
}

function Wait-ForMatrixCompletion {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)][string]$ReadyMarkerPath
    )
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $ReadyMarkerPath -PathType Leaf) {
            $marker = [IO.File]::ReadAllText($ReadyMarkerPath).Trim()
            if ($marker -eq 'R3_MATRIX_READY') {
                return
            }
            throw "Candidate wrote an invalid R3 ready marker: '$marker'."
        }
        if ($Process.HasExited) {
            throw "Candidate exited before publishing the R3 ready marker."
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Timed out waiting for the R3 ready marker before static sampling.'
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Path $runRoot | Out-Null

$qtPrefix = Resolve-QtPrefix -InstallerRoot $QtInstallerRoot
$qmakePath = Get-RequiredTool -Base $qtPrefix -RelativePath 'bin\qmake.exe' -Label 'qmake.exe'
$windeployqtPath = Get-RequiredTool -Base $qtPrefix -RelativePath 'bin\windeployqt.exe' -Label 'windeployqt.exe'
$qmakeVersion = (& $qmakePath -query QT_VERSION).Trim()
Assert-LastExitCode -Operation 'qmake version query'
if ($qmakeVersion -ne $expectedQtVersion) {
    throw "Expected Qt $expectedQtVersion, but qmake reports $qmakeVersion."
}
$cmake = Get-Command cmake -ErrorAction Stop
$cmakeVersionLine = (& $cmake.Source --version | Select-Object -First 1).Trim()
Assert-LastExitCode -Operation 'CMake version query'

& $validatorSelfTest
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "E5 validator self-test failed with exit code $LASTEXITCODE."
}

$canonicalBuild = Join-Path $runRoot 'build-canonical'
$firstArtifactRoot = Join-Path $runRoot 'first-build-artifact'
$exeA = Invoke-CleanBuild -BuildRoot $canonicalBuild -QtPrefix $qtPrefix -CMakePath $cmake.Source
$hashA = (Get-FileHash -LiteralPath $exeA -Algorithm SHA256).Hash
New-Item -ItemType Directory -Path $firstArtifactRoot | Out-Null
$firstArtifact = Join-Path $firstArtifactRoot 'ts-c01-qt-r2.exe'
Copy-Item -LiteralPath $exeA -Destination $firstArtifact
Remove-VerifiedCleanBuildRoot -BuildRoot $canonicalBuild -ExpectedParent $runRoot
$exeB = Invoke-CleanBuild -BuildRoot $canonicalBuild -QtPrefix $qtPrefix -CMakePath $cmake.Source
$hashB = (Get-FileHash -LiteralPath $exeB -Algorithm SHA256).Hash
if ($hashA -ne $hashB) {
    throw "Two clean Release builds at the canonical build path are not reproducible: $hashA / $hashB."
}
if ((Get-FileHash -LiteralPath $firstArtifact -Algorithm SHA256).Hash -ne $hashA) {
    throw 'Archived first clean-build artifact changed unexpectedly.'
}

$deployRoot = Join-Path $runRoot 'deploy'
New-Item -ItemType Directory -Path $deployRoot | Out-Null
$deployedExecutable = Join-Path $deployRoot 'ts-c01-qt-r2.exe'
Copy-Item -LiteralPath $exeB -Destination $deployedExecutable
& $windeployqtPath --release --no-translations --qmldir (Join-Path $scriptRoot 'qml') --dir $deployRoot $deployedExecutable
Assert-LastExitCode -Operation 'windeployqt R3 deployment'
$deployedHash = (Get-FileHash -LiteralPath $deployedExecutable -Algorithm SHA256).Hash
if ($deployedHash -ne $hashA) {
    throw 'Deployment changed the candidate executable.'
}

$deploymentFiles = @(Get-ChildItem -LiteralPath $deployRoot -Recurse -File | ForEach-Object {
    [ordered]@{
        relativePath = [IO.Path]::GetRelativePath($deployRoot, $_.FullName)
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
$runtimeBinaries = @($deploymentFiles | Where-Object {
    $_.relativePath -match '\.(dll|exe)$'
})
$attributionRoot = Join-Path $qtPrefix 'lib\cmake\Qt6\3rdparty'
$attributionFiles = @()
if (Test-Path -LiteralPath $attributionRoot -PathType Container) {
    $attributionFiles = @(Get-ChildItem -LiteralPath $attributionRoot -Recurse -Filter 'qt_attribution.json' -File | ForEach-Object {
        [ordered]@{
            path = $_.FullName
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
}
$licenseRoots = @(
    (Join-Path $qtPrefix 'LICENSES'),
    (Join-Path (Split-Path -Parent $qtPrefix) 'LICENSES'),
    (Join-Path $QtInstallerRoot 'Licenses')
)
$presentLicenseRoots = @($licenseRoots | Where-Object {
    Test-Path -LiteralPath $_ -PathType Container
})
$licenseStatus = if ($presentLicenseRoots.Count -gt 0) {
    'LICENSE_ROOTS_ENUMERATED'
}
else {
    'NO_TOP_LEVEL_QT_LICENSE_PAYLOAD_IN_BINARY_INSTALL_REVIEW_BEFORE_DISTRIBUTION'
}
$dependencyManifest = [ordered]@{
    schema = 'desktopup.ts-c01.r3-dependency-manifest.v1'
    candidateId = 'TS-C01'
    qtVersion = $qmakeVersion
    qtPrefix = $qtPrefix
    deploymentFileCount = $deploymentFiles.Count
    runtimeBinaryCount = $runtimeBinaries.Count
    files = $deploymentFiles
    qtAttributionMetadata = $attributionFiles
    licenseRoots = $presentLicenseRoots
    licenseMaterialStatus = $licenseStatus
    distributionReviewRequired = $true
}
$dependencyManifestPath = Join-Path $runRoot 'dependency-manifest.json'
$dependencyManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $dependencyManifestPath -Encoding UTF8

$normalLog = Join-Path $runRoot 'normal-diagnostics.jsonl'
$readyMarker = Join-Path $runRoot 'matrix-ready.txt'
$holdDurationMs = $StaticSampleDurationMs + 1000
$normalArgs = @(
    '--run-r2-fixture',
    '--r3-diagnostics',
    '--r3-ready-marker',
    $readyMarker,
    '--duration-ms',
    [string]$holdDurationMs,
    '--log',
    $normalLog
)
$process = Start-Process -FilePath $deployedExecutable -ArgumentList $normalArgs -WorkingDirectory $deployRoot -WindowStyle Hidden -PassThru
Wait-ForMatrixCompletion -Process $process -ReadyMarkerPath $readyMarker
$process.Refresh()
$cpuStartMs = $process.TotalProcessorTime.TotalMilliseconds
$lastCpuMs = $cpuStartMs
$workingSetSamples = [System.Collections.Generic.List[double]]::new()
$sampleTimer = [Diagnostics.Stopwatch]::StartNew()
while ($sampleTimer.ElapsedMilliseconds -lt $StaticSampleDurationMs) {
    if ($process.HasExited) {
        throw 'Candidate exited during static performance sampling.'
    }
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    $lastCpuMs = $process.TotalProcessorTime.TotalMilliseconds
    $workingSetSamples.Add($process.WorkingSet64 / 1MB)
}
$sampleTimer.Stop()
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    throw "R3 normal diagnostics exited with code $($process.ExitCode)."
}
if ($workingSetSamples.Count -lt 4) {
    throw 'Insufficient working-set samples.'
}
$cpuDeltaMs = [Math]::Max(0, $lastCpuMs - $cpuStartMs)
$singleCoreCpuPercent = if ($sampleTimer.Elapsed.TotalMilliseconds -gt 0) {
    100.0 * $cpuDeltaMs / $sampleTimer.Elapsed.TotalMilliseconds
}
else {
    0.0
}

& $validator -Path $normalLog
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "Normal candidate JSONL failed E5 validation with exit code $LASTEXITCODE."
}
$normalEvents = Read-Events -Path $normalLog
Assert-ZeroCleanup -Events $normalEvents -Label 'Normal diagnostics'
$staleRejected = @($normalEvents | Where-Object {
    $_.event -eq 'stale_frame_rejected' -and
    $_.rejection_reason -eq 'HOST_PROJECTION_REVISION_MISMATCH'
})
$lateRejected = @($normalEvents | Where-Object {
    $_.event -eq 'late_frame_rejected' -and
    $_.rejection_reason -eq 'ATTACHMENT_NOT_ACCEPTING_FRAMES'
})
$surfaceReady = @($normalEvents | Where-Object {
    $_.event -eq 'surface_ready' -and $_.graphics_api -eq 'Direct3D11'
})
$firstFrame = @($normalEvents | Where-Object { $_.event -eq 'frame_presented' })
$matrixComplete = @($normalEvents | Where-Object {
    $_.event -eq 'r2_fixture_matrix_completed' -and $_.result -eq 'PASS'
})
if ($staleRejected.Count -ne 1 -or $lateRejected.Count -ne 1 -or
    $surfaceReady.Count -ne 1 -or $firstFrame.Count -lt 1 -or
    $matrixComplete.Count -ne 1) {
    throw 'Normal R3 attach/frame/revision diagnostic coverage is incomplete.'
}
$latencySamples = @($normalEvents | Where-Object {
    $_.event -eq 'projection_latency_sample'
})
$latencyFailures = @($latencySamples | Where-Object {
    $_.budget_result -ne 'PASS' -or [double]$_.total_ms -gt 100.0
})
if ($latencySamples.Count -lt 10 -or $latencyFailures.Count -ne 0) {
    throw "R3 interaction latency budget failed: samples=$($latencySamples.Count), failures=$($latencyFailures.Count)."
}
$interactionLatency = [ordered]@{
    sampleCount = $latencySamples.Count
    budgetMs = 100
    maxCaptureMs = [Math]::Round(
        ($latencySamples | Measure-Object -Property capture_ms -Maximum).Maximum, 3)
    maxPublishMs = [Math]::Round(
        ($latencySamples | Measure-Object -Property publish_ms -Maximum).Maximum, 3)
    maxTotalMs = [Math]::Round(
        ($latencySamples | Measure-Object -Property total_ms -Maximum).Maximum, 3)
    result = 'PASS'
}
$framesAfterMatrix = @($firstFrame | Where-Object {
    $_.monotonic_ms -gt $matrixComplete[0].monotonic_ms
}).Count
$staticFps = 1000.0 * $framesAfterMatrix / $StaticSampleDurationMs
$workingSetMiBMin = [Math]::Round(
    ($workingSetSamples | Measure-Object -Minimum).Minimum, 3)
$workingSetMiBMax = [Math]::Round(
    ($workingSetSamples | Measure-Object -Maximum).Maximum, 3)
$workingSetMiBAvg = [Math]::Round(
    ($workingSetSamples | Measure-Object -Average).Average, 3)
$cpuBudgetResult = if ($cpuDeltaMs -le 100.0) { 'PASS' } else { 'FAIL' }
$workingSetBudgetResult = if ($workingSetMiBMax -le 50.0) { 'PASS' } else { 'FAIL' }
$staticFrameBudgetResult = if ($framesAfterMatrix -eq 0) { 'PASS' } else { 'FAIL' }
$performanceResult = if (
    $StaticSampleDurationMs -ge 10000 -and
    $cpuBudgetResult -eq 'PASS' -and
    $workingSetBudgetResult -eq 'PASS' -and
    $staticFrameBudgetResult -eq 'PASS') {
    'PASS'
}
else {
    'FAIL'
}
$performance = [ordered]@{
    sampleDurationMs = [Math]::Round($sampleTimer.Elapsed.TotalMilliseconds, 3)
    minimumRequiredSampleDurationMs = 10000
    sampleCount = $workingSetSamples.Count
    cpuTimeDeltaMs = [Math]::Round($cpuDeltaMs, 3)
    cpuTimeDeltaBudgetMs = 100
    cpuTimeDeltaBudgetResult = $cpuBudgetResult
    singleCoreCpuPercent = [Math]::Round($singleCoreCpuPercent, 3)
    workingSetMiBMin = $workingSetMiBMin
    workingSetMiBMax = $workingSetMiBMax
    workingSetMiBAvg = $workingSetMiBAvg
    workingSetBudgetMiB = 50
    workingSetBudgetResult = $workingSetBudgetResult
    framesAfterMatrix = $framesAfterMatrix
    staticFps = [Math]::Round($staticFps, 3)
    staticFrameBudgetResult = $staticFrameBudgetResult
    result = $performanceResult
}

$faultLog = Join-Path $runRoot 'fault-diagnostics.jsonl'
& $deployedExecutable --run-r2-fixture --r3-diagnostics --inject-surface-fault --duration-ms 250 --log $faultLog
Assert-LastExitCode -Operation 'R3 injected fault diagnostics'
& $validator -Path $faultLog
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "Fault candidate JSONL failed E5 validation with exit code $LASTEXITCODE."
}
$faultEvents = Read-Events -Path $faultLog
Assert-ZeroCleanup -Events $faultEvents -Label 'Injected fault diagnostics'
$fault = @($faultEvents | Where-Object {
    $_.event -eq 'surface_fault' -and
    $_.reason -eq 'R3_INJECTED_RENDERER_DEVICE_LOSS' -and
    $_.recoverable -eq $true
})
$faultLate = @($faultEvents | Where-Object {
    $_.event -eq 'late_frame_rejected'
})
if ($fault.Count -ne 1 -or $faultLate.Count -ne 1) {
    throw 'Injected fault lifecycle coverage is incomplete.'
}

$r4cStaticLog = Join-Path $runRoot 'r4c-static-lifecycle.jsonl'
$r4cStaticMarker = Join-Path $runRoot 'r4c-static-ready.txt'
& $deployedExecutable --run-r4c-manual --r3-diagnostics --duration-ms 1500 --log $r4cStaticLog --r3-ready-marker $r4cStaticMarker
Assert-LastExitCode -Operation 'R4-C static lifecycle diagnostics'
& $validator -Path $r4cStaticLog
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "R4-C static JSONL failed E5 validation with exit code $LASTEXITCODE."
}
$r4cStaticEvents = Read-Events -Path $r4cStaticLog
Assert-ZeroCleanup -Events $r4cStaticEvents -Label 'R4-C static lifecycle diagnostics'
$r4cMonitorReady = @($r4cStaticEvents | Where-Object {
    $_.event -eq 'r4c_shell_lifecycle_monitor_ready' -and
    $_.strategy -eq 'BOUNDED_SAFE_REDISCOVERY_NO_TOP_LEVEL_FALLBACK' -and
    $_.topmost -eq $false -and
    $_.ordinary_top_level_fallback -eq $false
})
$r4cProbeReady = @($r4cStaticEvents | Where-Object {
    $_.event -eq 'r4c_control_center_role_probe_ready' -and
    $_.window_role -eq 'CONTROL_CENTER_ROLE_PROBE' -and
    $_.product_control_center_implemented -eq $false
})
$r4cProbeReleased = @($r4cStaticEvents | Where-Object {
    $_.event -eq 'r4c_control_center_role_probe_released' -and
    [int]$_.live_probe_window_count -eq 0
})
$r4cFinished = @($r4cStaticEvents | Where-Object {
    $_.event -eq 'r4_manual_session_finished' -and
    $_.manual_phase -eq 'R4C' -and
    $_.result -eq 'PASS' -and
    [int]$_.shell_recovery_count -eq 0 -and
    [int]$_.shell_degraded_count -eq 0 -and
    $_.shell_degraded_at_finish -eq $false
})
$r4cUnexpectedLifecycle = @($r4cStaticEvents | Where-Object {
    $_.event -in @(
        'r4c_shell_generation_change_detected',
        'shell_generation_lost',
        'r4c_shell_recovery_cycle_started',
        'r4c_shell_recovery_budget_exhausted',
        'r4c_shell_lifecycle_failed'
    )
})
if ($r4cMonitorReady.Count -ne 1 -or
    $r4cProbeReady.Count -ne 1 -or
    $r4cProbeReleased.Count -ne 1 -or
    $r4cFinished.Count -ne 1 -or
    $r4cUnexpectedLifecycle.Count -ne 0) {
    throw 'R4-C static lifecycle/control-center role coverage is incomplete or produced a false recovery.'
}
$r4cStaticLifecycle = [ordered]@{
    result = 'PASS'
    monitorReady = $true
    controlCenterRoleProbeReady = $true
    productControlCenterImplemented = $false
    falseShellLossCount = 0
    recoveryCount = 0
    degradedCount = 0
    zeroResources = $true
    log = $r4cStaticLog
}

$r4dStaticLog = Join-Path $runRoot 'r4d-static-display-environment.jsonl'
$r4dStaticMarker = Join-Path $runRoot 'r4d-static-ready.txt'
& $deployedExecutable --run-r4d-manual --r3-diagnostics --duration-ms 1500 --log $r4dStaticLog --r3-ready-marker $r4dStaticMarker
Assert-LastExitCode -Operation 'R4-D1 static display-environment diagnostics'
& $validator -Path $r4dStaticLog
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "R4-D1 static JSONL failed E5 validation with exit code $LASTEXITCODE."
}
$r4dStaticEvents = Read-Events -Path $r4dStaticLog
Assert-ZeroCleanup -Events $r4dStaticEvents -Label 'R4-D1 static display-environment diagnostics'
$r4dMonitorReady = @($r4dStaticEvents | Where-Object {
    $_.event -eq 'r4d_display_environment_monitor_ready' -and
    $_.scope -eq 'PRIMARY_DISPLAY_ONLY' -and
    [int]$_.sample_interval_ms -eq 250 -and
    [int]$_.effective_dpi -ge 96 -and
    $_.multi_display -eq 'DEFERRED'
})
$r4dFinished = @($r4dStaticEvents | Where-Object {
    $_.event -eq 'r4_manual_session_finished' -and
    $_.manual_phase -eq 'R4D' -and
    $_.result -eq 'PASS' -and
    [int]$_.display_environment_change_count -eq 0 -and
    $_.display_environment_failed -eq $false
})
$r4dUnexpectedFailure = @($r4dStaticEvents | Where-Object {
    $_.event -eq 'r4d_display_environment_reprojection_failed'
})
if ($r4dMonitorReady.Count -ne 1 -or
    $r4dFinished.Count -ne 1 -or
    $r4dUnexpectedFailure.Count -ne 0) {
    throw 'R4-D1 static display-environment coverage is incomplete or produced a false change/failure.'
}
$r4dStaticDisplayEnvironment = [ordered]@{
    result = 'PASS'
    scope = 'PRIMARY_DISPLAY_ONLY'
    monitorReady = $true
    falseDisplayChangeCount = 0
    reprojectionFailureCount = 0
    multiDisplay = 'DEFERRED'
    zeroResources = $true
    log = $r4dStaticLog
}

$r4d2StaticLog = Join-Path $runRoot 'r4d2-static-theme-isolation.jsonl'
$r4d2StaticMarker = Join-Path $runRoot 'r4d2-static-ready.txt'
& $deployedExecutable --run-r4d2-manual --r4d2-self-test --r3-diagnostics --duration-ms 4000 --log $r4d2StaticLog --r3-ready-marker $r4d2StaticMarker
Assert-LastExitCode -Operation 'R4-D2 static theme/fallback/isolation diagnostics'
& $validator -Path $r4d2StaticLog
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "R4-D2 static JSONL failed E5 validation with exit code $LASTEXITCODE."
}
$r4d2StaticEvents = Read-Events -Path $r4d2StaticLog
Assert-ZeroCleanup -Events $r4d2StaticEvents -Label 'R4-D2 static theme/fallback/isolation diagnostics'
$r4d2Ready = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_controller_ready' -and
    $_.normal_theme_ids.Count -eq 3 -and
    $_.fallback_theme_id -eq 'minimal-fallback' -and
    $_.fallback_in_normal_selector -eq $false -and
    [int]$_.debounce_ms -eq 300 -and
    $_.self_test -eq $true
})
$r4d2Isolation = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_isolation_contract_ready' -and
    $_.payload_model -eq 'HOST_VALIDATED_BUILTIN_STRUCTURED_ENUM' -and
    $_.arbitrary_qml -eq $false -and
    $_.arbitrary_script -eq $false -and
    $_.native_code -eq $false -and
    $_.file_capability -eq $false -and
    $_.network_capability -eq $false -and
    $_.process_capability -eq $false -and
    $_.registry_capability -eq $false -and
    $_.credential_capability -eq $false -and
    $_.generic_ipc_capability -eq $false -and
    $_.host_owns_protected_interaction -eq $true -and
    $_.product_theme_package_platform_implemented -eq $false
})
$r4d2Committed = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_switch_committed'
})
$r4d2EffectiveThemes = @($r4d2Committed |
    ForEach-Object { [string]$_.effective_theme_id } |
    Sort-Object -Unique)
$r4d2GeometryKeys = @($r4d2Committed | ForEach-Object {
    "$($_.screen_rect_px -join ',')|$($_.presentation_mode)|$($_.interaction_mode)|$($_.is_locked)"
} | Sort-Object -Unique)
$r4d2Rejected = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_switch_rejected'
})
$r4d2FaultKinds = @($r4d2Rejected |
    ForEach-Object { [string]$_.fault_kind } |
    Sort-Object -Unique)
$r4d2LastKnownGood = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_fault_resolved' -and
    $_.resolution -eq 'LAST_KNOWN_GOOD_RETAINED' -and
    $_.projection_changed -eq $false -and
    $_.fallback_written_to_selection -eq $false
})
$r4d2Fallback = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_fault_resolved' -and
    $_.resolution -eq 'MINIMAL_FALLBACK_PRESENTED' -and
    $_.effective_theme_id -eq 'minimal-fallback' -and
    $_.selected_theme_id -ne 'minimal-fallback' -and
    $_.fallback_written_to_selection -eq $false
})
$r4d2SelfTest = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4d2_theme_self_test_completed' -and
    [int]$_.committed_count -eq 4 -and
    [int]$_.rejected_count -eq 4 -and
    [int]$_.fallback_count -eq 1 -and
    [int]$_.superseded_request_count -eq 2 -and
    $_.selected_theme_id -eq 'transparent-default' -and
    $_.effective_theme_id -eq 'transparent-default' -and
    $_.fallback_active -eq $false -and
    $_.result -eq 'PASS'
})
$r4d2Finished = @($r4d2StaticEvents | Where-Object {
    $_.event -eq 'r4_manual_session_finished' -and
    $_.manual_phase -eq 'R4D2' -and
    $_.result -eq 'PASS' -and
    [int]$_.theme_switch_committed_count -eq 4 -and
    [int]$_.theme_switch_rejected_count -eq 4 -and
    [int]$_.theme_fallback_count -eq 1 -and
    [int]$_.theme_superseded_request_count -eq 2 -and
    $_.theme_self_test_completed -eq $true -and
    $_.selected_theme_id -eq 'transparent-default' -and
    $_.effective_theme_id -eq 'transparent-default' -and
    $_.theme_fallback_active -eq $false
})
$r4d2Failures = @($r4d2StaticEvents | Where-Object {
    $_.event -in @(
        'r4d2_theme_controller_failed',
        'r4d2_theme_switch_failed'
    )
})
if ($r4d2Ready.Count -ne 1 -or
    $r4d2Isolation.Count -ne 1 -or
    $r4d2Committed.Count -ne 5 -or
    $r4d2EffectiveThemes.Count -ne 4 -or
    $r4d2EffectiveThemes -notcontains 'transparent-default' -or
    $r4d2EffectiveThemes -notcontains 'neon-hud' -or
    $r4d2EffectiveThemes -notcontains 'cinema-lightbox' -or
    $r4d2EffectiveThemes -notcontains 'minimal-fallback' -or
    @($r4d2Committed | Where-Object budget_result -ne 'PASS').Count -ne 0 -or
    $r4d2GeometryKeys.Count -ne 1 -or
    $r4d2Rejected.Count -ne 4 -or
    $r4d2FaultKinds.Count -ne 4 -or
    $r4d2LastKnownGood.Count -ne 3 -or
    $r4d2Fallback.Count -ne 1 -or
    $r4d2SelfTest.Count -ne 1 -or
    $r4d2Finished.Count -ne 1 -or
    $r4d2Failures.Count -ne 0) {
    throw 'R4-D2 theme/fallback/isolation coverage is incomplete or violated atomic/LKG/fallback invariants.'
}
$r4d2StaticThemeIsolation = [ordered]@{
    result = 'PASS'
    normalThemeCount = 3
    fallbackThemeId = 'minimal-fallback'
    committedProjectionCount = $r4d2Committed.Count
    rejectedFaultCount = $r4d2Rejected.Count
    lastKnownGoodRetentionCount = $r4d2LastKnownGood.Count
    fallbackPresentationCount = $r4d2Fallback.Count
    supersededRequestCount = 2
    atomicGeometryKeyCount = $r4d2GeometryKeys.Count
    isolationPayloadModel = 'HOST_VALIDATED_BUILTIN_STRUCTURED_ENUM'
    productThemePackagePlatformImplemented = $false
    zeroResources = $true
    log = $r4d2StaticLog
}

$r4d3StaticLog = Join-Path $runRoot 'r4d3-static-fault-performance.jsonl'
& $deployedExecutable --run-r4d3-manual --r4d3-self-test --r3-diagnostics --duration-ms 2500 --log $r4d3StaticLog
Assert-LastExitCode -Operation 'R4-D3 static fault/performance diagnostics'
& $validator -Path $r4d3StaticLog
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "R4-D3 static JSONL failed E5 validation with exit code $LASTEXITCODE."
}
$r4d3StaticEvents = Read-Events -Path $r4d3StaticLog
Assert-ZeroCleanup -Events $r4d3StaticEvents -Label 'R4-D3 static fault/performance diagnostics' -ExpectedPresentationReleaseCount 2
$r4d3Ready = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4d3_controller_ready' -and
    $_.self_test -eq $true -and
    $_.surface_recovery_scope -eq 'SURFACE_ONLY_SAME_ATTACHMENT' -and
    [int]$_.synthetic_item_count -eq 200 -and
    [int]$_.static_sample_seconds -eq 10 -and
    [int]$_.cpu_time_delta_budget_ms -eq 100 -and
    [int]$_.working_set_budget_mib -eq 50 -and
    [int]$_.theme_switch_budget_ms -eq 200 -and
    $_.topmost -eq $false -and
    $_.ordinary_top_level_fallback -eq $false
})
$r4d3StaleRevision = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4d3_stale_revision_probe' -and
    $_.rejection_error -eq 'STALE_FRAME_REJECTED' -and
    $_.result -eq 'PASS'
})
$r4d3DuplicateShell = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4d3_duplicate_shell_event_probe' -and
    $_.rejection_error -eq 'RECONNECT_NOT_REQUIRED' -and
    $_.attachment_unchanged -eq $true -and
    $_.result -eq 'PASS'
})
$r4d3Stress = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4d3_200_item_stress_completed' -and
    [int]$_.synthetic_item_count -eq 200 -and
    [int]$_.ranking_round_count -eq 64 -and
    [int]$_.theme_switch_count -eq 6 -and
    [double]$_.ranking_layout_ms -lt 200.0 -and
    [double]$_.max_theme_switch_ms -lt 200.0 -and
    $_.geometry_preserved -eq $true -and
    $_.core_interaction_blocked -eq $false -and
    $_.result -eq 'PASS'
})
$r4d3Fault = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'surface_fault' -and
    $_.reason -eq 'R4D3_INJECTED_RECOVERABLE_RENDERER_DEVICE_LOSS' -and
    $_.recoverable -eq $true -and
    $_.recovery_scope -eq 'SURFACE_ONLY_SAME_ATTACHMENT' -and
    $_.topmost -eq $false -and
    $_.ordinary_top_level_fallback -eq $false
})
$r4d3RecoveryReady = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'surface_recovery_ready' -and
    $_.attachment_preserved -eq $true -and
    $_.shell_generation_preserved -eq $true -and
    $_.lifecycle_epoch_preserved -eq $true -and
    $_.surface_epoch_advanced_once -eq $true -and
    $_.result -eq 'OK'
})
$r4d3OldSurfaceRejected = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'stale_frame_rejected' -and
    $_.rejection_reason -eq 'SURFACE_EPOCH_MISMATCH' -and
    [long]$_.observed_surface_epoch -lt [long]$_.expected_surface_epoch -and
    $_.result -eq 'PASS'
})
$r4d3RecoveryCompleted = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4d3_surface_recovery_completed' -and
    $_.attachment_preserved -eq $true -and
    $_.shell_generation_preserved -eq $true -and
    $_.lifecycle_epoch_preserved -eq $true -and
    $_.surface_epoch_advanced_once -eq $true -and
    $_.state_preserved -eq $true -and
    $_.old_surface_frame_rejected -eq $true -and
    $_.topmost -eq $false -and
    $_.ordinary_top_level_fallback -eq $false -and
    $_.result -eq 'PASS'
})
$r4d3SelfTest = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4d3_self_test_completed' -and
    [int]$_.recovery_count -eq 1 -and
    [int]$_.stale_reject_count -eq 1 -and
    [int]$_.duplicate_shell_reject_count -eq 1 -and
    [int]$_.stress_count -eq 1 -and
    [double]$_.max_theme_switch_ms -lt 200.0 -and
    $_.result -eq 'PASS'
})
$r4d3Finished = @($r4d3StaticEvents | Where-Object {
    $_.event -eq 'r4_manual_session_finished' -and
    $_.manual_phase -eq 'R4D3' -and
    [int]$_.r4d3_recovery_count -eq 1 -and
    [int]$_.r4d3_stale_reject_count -eq 1 -and
    [int]$_.r4d3_duplicate_shell_reject_count -eq 1 -and
    [int]$_.r4d3_stress_count -eq 1 -and
    $_.r4d3_self_test_completed -eq $true -and
    $_.result -eq 'PASS'
})
$r4d3Failures = @($r4d3StaticEvents | Where-Object {
    $_.event -in @(
        'r4d3_controller_failed',
        'surface_recovery_failed'
    )
})
if ($r4d3Ready.Count -ne 1 -or
    $r4d3StaleRevision.Count -ne 1 -or
    $r4d3DuplicateShell.Count -ne 1 -or
    $r4d3Stress.Count -ne 1 -or
    $r4d3Fault.Count -ne 1 -or
    $r4d3RecoveryReady.Count -ne 1 -or
    $r4d3OldSurfaceRejected.Count -ne 1 -or
    $r4d3RecoveryCompleted.Count -ne 1 -or
    $r4d3SelfTest.Count -ne 1 -or
    $r4d3Finished.Count -ne 1 -or
    $r4d3Failures.Count -ne 0) {
    throw 'R4-D3 fault/recovery/stale-event/200-Item coverage is incomplete or violated frozen invariants.'
}
$r4d3GateResult = if ($performance.result -eq 'PASS') { 'PASS' } else { 'FAIL' }
$r4d3StaticFaultPerformance = [ordered]@{
    result = $r4d3GateResult
    functionalResult = 'PASS'
    surfaceRecoveryScope = 'SURFACE_ONLY_SAME_ATTACHMENT'
    recoveryCount = 1
    attachmentPreserved = $true
    shellGenerationPreserved = $true
    lifecycleEpochPreserved = $true
    surfaceEpochAdvancedOnce = $true
    oldSurfaceFrameRejected = $true
    staleRevisionRejected = $true
    duplicateCurrentShellEventRejected = $true
    syntheticItemCount = 200
    rankingRoundCount = 64
    rankingLayoutMs = [Math]::Round([double]$r4d3Stress[0].ranking_layout_ms, 3)
    themeSwitchCount = 6
    maxThemeSwitchMs = [Math]::Round([double]$r4d3Stress[0].max_theme_switch_ms, 3)
    coreInteractionBlocked = $false
    performance = $performance
    zeroResources = $true
    log = $r4d3StaticLog
}
$manualLog = Join-Path $outputRoot 'r2-manual-retest-user.jsonl'
$manualLogHash = if (Test-Path -LiteralPath $manualLog -PathType Leaf) {
    (Get-FileHash -LiteralPath $manualLog -Algorithm SHA256).Hash
}
else {
    $null
}
$summary = [ordered]@{
    schema = 'desktopup.ts-c01.r3-preflight.v1'
    candidateId = 'TS-C01'
    scope = 'R3_AUTOMATED_PREFLIGHT_P0_NOT_RUN'
    result = $r4d3GateResult
    timestamp = (Get-Date).ToString('o')
    qtPrefix = $qtPrefix
    qtVersion = $qmakeVersion
    cmake = $cmake.Source
    cmakeVersion = $cmakeVersionLine
    generator = $expectedGenerator
    toolset = $expectedToolset
    windowsSdk = $expectedWindowsSdk
    configuration = 'Release'
    sourceDateEpoch = $sourceDateEpoch
    transport = 'OFFSCREEN_FRAME_SOURCE'
    ownership = 'HOST'
    cleanBuildCount = 2
    cleanBuildReproducible = $true
    buildAExecutableSha256 = $hashA
    buildBExecutableSha256 = $hashB
    deployedExecutable = $deployedExecutable
    deployedExecutableSha256 = $deployedHash
    dependencyManifest = $dependencyManifestPath
    dependencyFileCount = $deploymentFiles.Count
    runtimeBinaryCount = $runtimeBinaries.Count
    licenseMaterialStatus = $licenseStatus
    distributionLicenseReviewRequired = $true
    e5ValidatorSelfTest = 'PASS'
    normalJsonlValidation = 'PASS'
    faultJsonlValidation = 'PASS'
    attachFirstFrame = 'PASS'
    staleRevisionRejected = 'PASS'
    lateFrameRejected = 'PASS'
    injectedFaultDetachExitCleanup = 'PASS'
    zeroResources = 'PASS'
    interactionLatency = $interactionLatency
    performance = $performance
    r4cStaticLifecycle = $r4cStaticLifecycle
    r4dStaticDisplayEnvironment = $r4dStaticDisplayEnvironment
    r4d2StaticThemeIsolation = $r4d2StaticThemeIsolation
    r4d3StaticFaultPerformance = $r4d3StaticFaultPerformance
    normalLog = $normalLog
    readyMarker = $readyMarker
    faultLog = $faultLog
    r2ManualGate = 'USER_CONFIRMED_PASS_1_TO_6'
    r2ManualLog = if ($manualLogHash) { $manualLog } else { $null }
    r2ManualLogSha256 = $manualLogHash
    candidateEvidenceCreated = $false
    p0Result = 'NOT_RUN'
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Host "Summary: $summaryPath"
Write-Host "Run root: $runRoot"
if ($r4d3GateResult -ne 'PASS') {
    Write-Error "TS-C01 R4-D3 hard gate FAIL: CPU=$($performance.cpuTimeDeltaMs)/$($performance.cpuTimeDeltaBudgetMs) ms; working set=$($performance.workingSetMiBMax)/$($performance.workingSetBudgetMiB) MiB; frames=$($performance.framesAfterMatrix). Candidate Evidence and P0 remain NOT RUN."
    exit 1
}
Write-Host 'TS-C01 R3/R4-D3 automated preflight PASS. R4-D3 manual observation remains pending; P0 remains NOT RUN.'
