[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$QtInstallerRoot
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
$summaryPath = Join-Path $outputRoot 'entry-preflight.json'

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
        [Parameter(Mandatory)][string[]]$RelativeCandidates,
        [Parameter(Mandatory)][string]$Label
    )

    foreach ($relativePath in $RelativeCandidates) {
        $candidate = Join-Path $Base $relativePath
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "$Label was not found in Qt prefix '$Base'."
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$qtPrefix = Resolve-QtPrefix -InstallerRoot $QtInstallerRoot
$qmakePath = Get-RequiredTool -Base $qtPrefix -RelativeCandidates @('bin\qmake.exe') -Label 'qmake.exe'
$qtpathsPath = Get-RequiredTool -Base $qtPrefix -RelativeCandidates @('bin\qtpaths.exe', 'bin\qtpaths6.exe') -Label 'qtpaths.exe'
$windeployqtPath = Get-RequiredTool -Base $qtPrefix -RelativeCandidates @('bin\windeployqt.exe') -Label 'windeployqt.exe'
$qtConfigPath = Get-RequiredTool -Base $qtPrefix -RelativeCandidates @('lib\cmake\Qt6\Qt6Config.cmake') -Label 'Qt6Config.cmake'

$qmakeVersion = (& $qmakePath -query QT_VERSION).Trim()
Assert-LastExitCode -Operation 'qmake version query'
if ($qmakeVersion -ne $expectedQtVersion) {
    throw "Expected Qt $expectedQtVersion, but qmake reports $qmakeVersion."
}

$qtpathsVersion = (& $qtpathsPath --query QT_VERSION).Trim()
Assert-LastExitCode -Operation 'qtpaths version query'
if ($qtpathsVersion -ne $expectedQtVersion) {
    throw "Expected Qt $expectedQtVersion, but qtpaths reports $qtpathsVersion."
}

$cmake = Get-Command cmake -ErrorAction Stop
$cmakeVersionLine = (& $cmake.Source --version | Select-Object -First 1).Trim()
Assert-LastExitCode -Operation 'CMake version query'

$e5Validator = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..\desktop-host-win32\ts-h01-e5\test-validator.ps1')).Path
& $e5Validator
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "E5 validator self-test failed with exit code $LASTEXITCODE."
}

Push-Location $scriptRoot
try {
    & $cmake.Source --preset x64-release "-DCMAKE_PREFIX_PATH=$qtPrefix"
    Assert-LastExitCode -Operation 'CMake configure'

    & $cmake.Source --build --preset x64-release
    Assert-LastExitCode -Operation 'CMake Release build'
}
finally {
    Pop-Location
}

$cachePath = Join-Path $buildRoot 'CMakeCache.txt'
if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
    throw "CMake cache was not produced at '$cachePath'."
}

$cacheText = Get-Content -LiteralPath $cachePath -Raw
if ($cacheText -notmatch [regex]::Escape('CMAKE_GENERATOR_TOOLSET:INTERNAL=v143,version=14.44,host=x64')) {
    throw 'Configured generator toolset is not the frozen v143 / 14.44 / host=x64 value.'
}
$systemVersionPattern = "(?m)^CMAKE_SYSTEM_VERSION:[^=]+=$([regex]::Escape($expectedWindowsSdk))\r?$"
if ($cacheText -notmatch $systemVersionPattern) {
    throw "Configured Windows SDK is not $expectedWindowsSdk."
}

$builtExecutable = Join-Path $buildRoot 'Release\ts-c01-qt-entry.exe'
if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
    throw "Release executable was not produced at '$builtExecutable'."
}

$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$deployRoot = Join-Path $outputRoot "entry-deploy-$runId"
New-Item -ItemType Directory -Path $deployRoot | Out-Null
$deployedExecutable = Join-Path $deployRoot 'ts-c01-qt-entry.exe'
Copy-Item -LiteralPath $builtExecutable -Destination $deployedExecutable

& $windeployqtPath --release --no-translations --qmldir (Join-Path $scriptRoot 'qml') --dir $deployRoot $deployedExecutable
Assert-LastExitCode -Operation 'windeployqt deployment'

& $deployedExecutable --self-check
Assert-LastExitCode -Operation 'deployed Qt Quick self-check'

$summary = [ordered]@{
    schema = 'desktopup.ts-c01.entry-preflight.v1'
    candidateId = 'TS-C01'
    scope = 'R0_ENTRY_ONLY_P0_NOT_RUN'
    result = 'PASS'
    timestamp = (Get-Date).ToString('o')
    qtInstallerRoot = (Resolve-Path -LiteralPath $QtInstallerRoot).Path
    qtPrefix = $qtPrefix
    qtVersion = $qmakeVersion
    qmake = $qmakePath
    qtpaths = $qtpathsPath
    windeployqt = $windeployqtPath
    qt6Config = $qtConfigPath
    cmake = $cmake.Source
    cmakeVersion = $cmakeVersionLine
    generator = $expectedGenerator
    toolset = $expectedToolset
    windowsSdk = $expectedWindowsSdk
    configuration = 'Release'
    deployedExecutable = $deployedExecutable
    p0Result = 'NOT_RUN'
}

$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host 'TS-C01 R0 entry preflight PASS. P0 remains NOT RUN.'
Write-Host "Summary: $summaryPath"
