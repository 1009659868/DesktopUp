[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$QtInstallerRoot,

    [Parameter()]
    [ValidateRange(250, 15000)]
    [int]$DurationMs = 1200
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
$summaryPath = Join-Path $outputRoot 'r1-preflight.json'
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

    & $cmake.Source --build --preset x64-release --target ts-c01-qt-r1
    Assert-LastExitCode -Operation 'CMake R1 Release build'
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

$builtExecutable = Join-Path $buildRoot 'Release\ts-c01-qt-r1.exe'
if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
    throw "R1 Release executable was not produced at '$builtExecutable'."
}

$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$deployRoot = Join-Path $outputRoot "r1-deploy-$runId"
New-Item -ItemType Directory -Path $deployRoot | Out-Null
$deployedExecutable = Join-Path $deployRoot 'ts-c01-qt-r1.exe'
Copy-Item -LiteralPath $builtExecutable -Destination $deployedExecutable

& $windeployqtPath --release --no-translations --qmldir (Join-Path $scriptRoot 'qml') --dir $deployRoot $deployedExecutable
Assert-LastExitCode -Operation 'windeployqt R1 deployment'

$eventPath = Join-Path $outputRoot "r1-events-$runId.jsonl"
& $deployedExecutable --run-r1-lifecycle --duration-ms $DurationMs --log $eventPath
Assert-LastExitCode -Operation 'deployed R1 lifecycle run'

& $validator -Path $eventPath
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "E5 event validation failed with exit code $LASTEXITCODE."
}

$events = @(Get-Content -LiteralPath $eventPath | ForEach-Object { $_ | ConvertFrom-Json -Depth 20 })
$surfaceReady = @($events | Where-Object { $_.event -eq 'surface_ready' })
$frames = @($events | Where-Object { $_.event -eq 'frame_presented' })
$detached = @($events | Where-Object {
    $_.event -eq 'attachment_state' -and $_.host_state -eq 'DETACHED' -and $_.surface_state -eq 'RELEASED'
})
$cleanup = @($events | Where-Object {
    $_.event -eq 'cleanup_completed' -and
    $_.resource_counts.host -eq 0 -and
    $_.resource_counts.adapter -eq 0 -and
    $_.resource_counts.quarantine -eq 0
})
if ($surfaceReady.Count -ne 1 -or $surfaceReady[0].graphics_api -ne 'Direct3D11') {
    throw 'R1 did not report exactly one Direct3D11 surface_ready event.'
}
if ($frames.Count -lt 1) {
    throw 'R1 did not report a presented frame.'
}
if ($detached.Count -lt 1) {
    throw 'R1 did not reach DETACHED / RELEASED.'
}
if ($cleanup.Count -ne 1) {
    throw 'R1 did not report exactly one zero-resource cleanup.'
}

$executableHash = (Get-FileHash -LiteralPath $deployedExecutable -Algorithm SHA256).Hash
$summary = [ordered]@{
    schema = 'desktopup.ts-c01.r1-preflight.v1'
    candidateId = 'TS-C01'
    scope = 'R1_ADAPTER_CONTRACT_P0_NOT_RUN'
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
    graphicsApi = $surfaceReady[0].graphics_api
    surfaceTransport = $events[0].surface_transport
    surfaceOwnership = $events[0].surface_ownership
    durationMs = $DurationMs
    eventCount = $events.Count
    firstFrameCount = $frames.Count
    detachedCount = $detached.Count
    zeroCleanupCount = $cleanup.Count
    deployedExecutable = $deployedExecutable
    deployedExecutableSha256 = $executableHash
    eventPath = $eventPath
    e5Validation = 'PASS'
    p0Result = 'NOT_RUN'
}

$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host 'TS-C01 R1 adapter-contract preflight PASS. P0 remains NOT RUN.'
Write-Host "Summary: $summaryPath"
Write-Host "Events: $eventPath"
