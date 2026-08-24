[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$QtInstallerRoot,

    [Parameter()]
    [ValidateRange(2000, 10000)]
    [int]$StaticSampleDurationMs = 3000
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
        [Parameter(Mandatory)][string]$Label
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
    if ($cleanup.Count -ne 1 -or $visual.Count -ne 1 -or $islands.Count -ne 1) {
        throw "$Label did not report exactly one zero-resource cleanup."
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
$framesAfterMatrix = @($firstFrame | Where-Object {
    $_.monotonic_ms -gt $matrixComplete[0].monotonic_ms
}).Count
$staticFps = 1000.0 * $framesAfterMatrix / $StaticSampleDurationMs
$performance = [ordered]@{
    sampleDurationMs = [Math]::Round($sampleTimer.Elapsed.TotalMilliseconds, 3)
    sampleCount = $workingSetSamples.Count
    singleCoreCpuPercent = [Math]::Round($singleCoreCpuPercent, 3)
    workingSetMiBMin = [Math]::Round(($workingSetSamples | Measure-Object -Minimum).Minimum, 3)
    workingSetMiBMax = [Math]::Round(($workingSetSamples | Measure-Object -Maximum).Maximum, 3)
    workingSetMiBAvg = [Math]::Round(($workingSetSamples | Measure-Object -Average).Average, 3)
    framesAfterMatrix = $framesAfterMatrix
    staticFps = [Math]::Round($staticFps, 3)
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
    result = 'PASS'
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
    performance = $performance
    normalLog = $normalLog
    readyMarker = $readyMarker
    faultLog = $faultLog
    r2ManualGate = 'USER_CONFIRMED_PASS_1_TO_6'
    r2ManualLog = if ($manualLogHash) { $manualLog } else { $null }
    r2ManualLogSha256 = $manualLogHash
    candidateEvidenceCreated = $false
    p0Result = 'NOT_RUN'
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Host 'TS-C01 R3 automated preflight PASS. R4 manual P0 remains pending; P0 remains NOT RUN.'
Write-Host "Summary: $summaryPath"
Write-Host "Run root: $runRoot"
