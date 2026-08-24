[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$QtInstallerRoot,

    [Parameter()]
    [ValidateRange(250, 60000)]
    [int]$DurationMs = 1500
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedQtVersion = '6.8.3'
$expectedWindowsSdk = '10.0.26100.0'
$expectedGenerator = 'Visual Studio 18 2026'
$expectedToolset = 'v143,version=14.44,host=x64'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputRoot = Join-Path $scriptRoot 'out'
$buildRoot = Join-Path $outputRoot 'entry-build'
$summaryPath = Join-Path $outputRoot 'r2-preflight.json'
$e5Root = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..\desktop-host-win32\ts-h01-e5')).Path

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
    throw "Qt 6.8.3 MSVC 2022 x64 prefix is not complete under bounded root '$InstallerRoot'."
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

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

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

$validatorSelfTest = Join-Path $e5Root 'test-validator.ps1'
$validator = Join-Path $e5Root 'validate-desktop-host-events.ps1'
& $validatorSelfTest
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "E5 validator self-test failed with exit code $LASTEXITCODE."
}

Push-Location $scriptRoot
try {
    & $cmake.Source --preset x64-release "-DCMAKE_PREFIX_PATH=$qtPrefix"
    Assert-LastExitCode -Operation 'CMake configure'

    & $cmake.Source --build --preset x64-release --target ts-c01-qt-r2
    Assert-LastExitCode -Operation 'CMake R2 Release build'
}
finally {
    Pop-Location
}

$cachePath = Join-Path $buildRoot 'CMakeCache.txt'
$cacheText = Get-Content -LiteralPath $cachePath -Raw
if ($cacheText -notmatch [regex]::Escape('CMAKE_GENERATOR_TOOLSET:INTERNAL=v143,version=14.44,host=x64')) {
    throw 'Configured generator toolset is not the frozen v143 / 14.44 / host=x64 value.'
}
$systemVersionPattern = "(?m)^CMAKE_SYSTEM_VERSION:[^=]+=$([regex]::Escape($expectedWindowsSdk))\r?$"
if ($cacheText -notmatch $systemVersionPattern) {
    throw "Configured Windows SDK is not $expectedWindowsSdk."
}

$builtExecutable = Join-Path $buildRoot 'Release\ts-c01-qt-r2.exe'
if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
    throw "R2 Release executable was not produced at '$builtExecutable'."
}

$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$deployRoot = Join-Path $outputRoot "r2-deploy-$runId"
New-Item -ItemType Directory -Path $deployRoot | Out-Null
$deployedExecutable = Join-Path $deployRoot 'ts-c01-qt-r2.exe'
Copy-Item -LiteralPath $builtExecutable -Destination $deployedExecutable

& $windeployqtPath --release --no-translations --qmldir (Join-Path $scriptRoot 'qml') --dir $deployRoot $deployedExecutable
Assert-LastExitCode -Operation 'windeployqt R2 deployment'

$eventPath = Join-Path $outputRoot "r2-events-$runId.jsonl"
& $deployedExecutable --run-r2-fixture --duration-ms $DurationMs --log $eventPath
Assert-LastExitCode -Operation 'deployed R2 fixture run'

& $validator -Path $eventPath
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "E5 event validation failed with exit code $LASTEXITCODE."
}

$events = @(Get-Content -LiteralPath $eventPath | ForEach-Object { $_ | ConvertFrom-Json -Depth 24 })
$projections = @($events | Where-Object { $_.event -eq 'projection_committed' })
$frames = @($events | Where-Object { $_.event -eq 'frame_presented' })
$matrixSamples = @($events | Where-Object { $_.event -eq 'hit_matrix_sample' })
$matrixComplete = @($events | Where-Object {
    $_.event -eq 'r2_fixture_matrix_completed' -and $_.result -eq 'PASS'
})
$visualReady = @($events | Where-Object {
    $_.event -eq 'host_layered_visual_ready' -and
    $_.surface_transport -eq 'OFFSCREEN_FRAME_SOURCE' -and
    $_.surface_ownership -eq 'HOST' -and
    $_.live_visual_count -eq 1 -and
    $_.unexpected_visual_pointer_count -eq 0
})
$visualReleased = @($events | Where-Object {
    $_.event -eq 'host_layered_visual_released' -and
    $_.live_visual_count -eq 0 -and
    $_.unexpected_visual_pointer_count -eq 0
})
$islandsReleased = @($events | Where-Object {
    $_.event -eq 'input_islands_released' -and $_.live_island_count -eq 0
})
$lockReject = @($events | Where-Object {
    $_.event -eq 'geometry_gesture_cancelled' -and
    $_.cancel_reason -eq 'LOCKED_OPERATION_REJECTED'
})
$moveEnded = @($events | Where-Object {
    $_.event -eq 'geometry_gesture_ended' -and $_.gesture_kind -eq 'MOVE'
})
$resizeEnded = @($events | Where-Object {
    $_.event -eq 'geometry_gesture_ended' -and
    $_.gesture_kind -eq 'RESIZE_BOTTOM_RIGHT'
})
$cleanup = @($events | Where-Object {
    $_.event -eq 'cleanup_completed' -and
    $_.resource_counts.host -eq 0 -and
    $_.resource_counts.adapter -eq 0 -and
    $_.resource_counts.quarantine -eq 0
})

$expectedHitCounts = @{
    ZERO = 0
    ONE = 1
    MULTI = 3
    OVERLAP = 2
}
foreach ($scenario in $expectedHitCounts.Keys) {
    $sample = @($matrixSamples | Where-Object {
        $_.scenario -eq $scenario -and
        $_.matrix_result -eq 'PASS' -and
        $_.published_region_count -eq $expectedHitCounts[$scenario]
    })
    if ($sample.Count -ne 1) {
        throw "R2 hit scenario '$scenario' did not produce exactly one expected PASS sample."
    }
}
if ($matrixComplete.Count -ne 1) {
    throw 'R2 fixture matrix did not complete exactly once.'
}
if ($projections.Count -ne $frames.Count) {
    throw "Projection/frame count mismatch: $($projections.Count) / $($frames.Count)."
}
if ($visualReady.Count -ne 1 -or $visualReleased.Count -ne 1) {
    throw 'R2 Host layered visual route or zero-pointer cleanup contract failed.'
}
if ($islandsReleased.Count -ne 1) {
    throw 'R2 input islands were not released exactly once with zero live HWND.'
}
if ($lockReject.Count -ne 1 -or $moveEnded.Count -ne 1 -or $resizeEnded.Count -ne 1) {
    throw 'R2 lock/move/resize gesture coverage is incomplete.'
}
if ($cleanup.Count -ne 1) {
    throw 'R2 did not report exactly one zero-resource cleanup.'
}

$collapsed = @($projections | Where-Object {
    $null -ne $_.PSObject.Properties['projection_reason'] -and
    $null -ne $_.PSObject.Properties['presentation_mode'] -and
    $_.projection_reason -eq 'PRESENTATION_COLLAPSE' -and
    $_.presentation_mode -eq 'COLLAPSED'
})
$expanded = @($projections | Where-Object {
    $null -ne $_.PSObject.Properties['projection_reason'] -and
    $null -ne $_.PSObject.Properties['presentation_mode'] -and
    $_.projection_reason -eq 'PRESENTATION_EXPAND' -and
    $_.presentation_mode -eq 'EXPANDED'
})
if ($collapsed.Count -ne 1 -or $expanded.Count -ne 1) {
    throw 'R2 collapse/expand projections are incomplete.'
}
if ($collapsed[0].logical_geometry.x -ne $expanded[0].logical_geometry.x -or
    $collapsed[0].logical_geometry.y -ne $expanded[0].logical_geometry.y -or
    $collapsed[0].logical_geometry.width -ne $expanded[0].logical_geometry.width) {
    throw 'R2 collapse changed top/left/width.'
}

$executableHash = (Get-FileHash -LiteralPath $deployedExecutable -Algorithm SHA256).Hash
$summary = [ordered]@{
    schema = 'desktopup.ts-c01.r2-preflight.v1'
    candidateId = 'TS-C01'
    scope = 'R2_NIGHT_OPS_FIXTURE_P0_NOT_RUN'
    result = 'PASS'
    timestamp = (Get-Date).ToString('o')
    qtInstallerRoot = (Resolve-Path -LiteralPath $QtInstallerRoot).Path
    qtPrefix = $qtPrefix
    qtVersion = $qmakeVersion
    cmake = $cmake.Source
    cmakeVersion = $cmakeVersionLine
    generator = $expectedGenerator
    toolset = $expectedToolset
    windowsSdk = $expectedWindowsSdk
    configuration = 'Release'
    graphicsApi = 'Direct3D11'
    surfaceTransport = 'OFFSCREEN_FRAME_SOURCE'
    surfaceOwnership = 'HOST'
    inputStrategy = 'QT_OFFSCREEN_FRAME_PLUS_HOST_LAYERED_TRANSPARENT_VISUAL_AND_SIBLING_ISLANDS'
    visualBlankEvidence = 'AUTOMATED_PARENT_INTERNAL_PASS_MANUAL_EXPLORER_GATE_PENDING'
    durationMs = $DurationMs
    eventCount = $events.Count
    projectionCount = $projections.Count
    frameCount = $frames.Count
    matrixSampleCount = $matrixSamples.Count
    scenarios = @('ZERO','ONE','MULTI','OVERLAP','COLLAPSE_EXPAND','LOCK_REJECT','EDITING_MOVE','EDITING_RESIZE','FINAL_USING')
    zeroLayeredVisualCleanup = $true
    zeroUnexpectedVisualPointer = $true
    zeroIslandCleanup = $true
    zeroHostAdapterCleanup = $true
    deployedExecutable = $deployedExecutable
    deployedExecutableSha256 = $executableHash
    eventPath = $eventPath
    e5Validation = 'PASS'
    p0Result = 'NOT_RUN'
}

$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host 'TS-C01 R2 Night Ops fixture preflight PASS. Explorer-native manual input gate remains pending; P0 remains NOT RUN.'
Write-Host "Summary: $summaryPath"
Write-Host "Events: $eventPath"
