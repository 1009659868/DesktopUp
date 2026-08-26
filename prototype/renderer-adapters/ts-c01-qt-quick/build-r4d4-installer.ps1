param(
    [Parameter(Mandatory = $true)]
    [string]$QtPrefix,
    [string]$MakensisPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputRoot = Join-Path $scriptRoot 'out'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $outputRoot "r4d4-run-$runId"
$buildCanonical = Join-Path $runRoot 'build-canonical'
$firstArtifactRoot = Join-Path $runRoot 'first-build-artifact'
$payload = Join-Path $runRoot 'payload'
$resources = Join-Path $payload 'resources'
$packageRoot = Join-Path $outputRoot 'r4d4-package'
$installerCandidate = Join-Path $runRoot 'DesktopUpSetup.exe'
$installGuardInclude = Join-Path $runRoot 'install-guard-r4d4-generated.nsh'
$uninstallInclude = Join-Path $runRoot 'uninstall-r4d4-generated.nsh'
$summaryPath = Join-Path $runRoot 'r4d4-preflight.json'
$stableSummaryPath = Join-Path $packageRoot 'r4d4-preflight.json'
$stableInstallerPath = Join-Path $packageRoot 'DesktopUpSetup.exe'
$cmake = (Get-Command cmake.exe -ErrorAction Stop).Source
$windeployqt = Join-Path $QtPrefix 'bin\windeployqt.exe'
$nsi = Join-Path $scriptRoot 'installer-r4d4.nsi'
$buildId = 'ts-c01-qt-6.8.3-r4d4-20260826-02'
$sourceDateEpoch = '1704067200'
$windowsSdk = '10.0.26100.0'

if ([string]::IsNullOrWhiteSpace($MakensisPath)) {
    $makensisCommand = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($null -ne $makensisCommand) {
        $MakensisPath = $makensisCommand.Source
    } else {
        $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
        if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
            $standardMakensis = Join-Path $programFilesX86 'NSIS\makensis.exe'
            if (Test-Path -LiteralPath $standardMakensis -PathType Leaf) {
                $MakensisPath = $standardMakensis
            }
        }
    }
}

foreach ($required in @($windeployqt, $MakensisPath, $nsi)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required R4-D4 tool/input is missing: $required"
    }
}
New-Item -ItemType Directory -Path $runRoot, $payload, $resources -Force | Out-Null

function Invoke-CleanBuild([string]$buildRoot) {
    & $cmake -E env "SOURCE_DATE_EPOCH=$sourceDateEpoch" $cmake -S $scriptRoot -B $buildRoot -G 'Visual Studio 18 2026' -A x64 -T 'v143,version=14.44,host=x64' "-DCMAKE_SYSTEM_VERSION=$windowsSdk" "-DCMAKE_PREFIX_PATH=$QtPrefix" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "CMake configure failed for $buildRoot" }
    & $cmake -E env "SOURCE_DATE_EPOCH=$sourceDateEpoch" $cmake --build $buildRoot --config Release --target ts-c01-qt-r2 --parallel 8 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "CMake Release build failed for $buildRoot" }
    $exe = Join-Path $buildRoot 'Release\ts-c01-qt-r2.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Release EXE missing: $exe" }
    return (Resolve-Path -LiteralPath $exe).Path
}

$exeA = Invoke-CleanBuild $buildCanonical
$hashA = (Get-FileHash -LiteralPath $exeA -Algorithm SHA256).Hash
New-Item -ItemType Directory -Path $firstArtifactRoot | Out-Null
$firstArtifact = Join-Path $firstArtifactRoot 'ts-c01-qt-r2.exe'
Copy-Item -LiteralPath $exeA -Destination $firstArtifact
$runRootFull = [IO.Path]::GetFullPath($runRoot).TrimEnd('\')
$canonicalFull = [IO.Path]::GetFullPath($buildCanonical).TrimEnd('\')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetDirectoryName($canonicalFull), $runRootFull) -or
    [IO.Path]::GetFileName($canonicalFull) -ne 'build-canonical') {
    throw "Refusing to remove unexpected build root: $canonicalFull"
}
$resolvedCanonical = (Resolve-Path -LiteralPath $canonicalFull).Path.TrimEnd('\')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals($resolvedCanonical, $canonicalFull)) {
    throw "Resolved build root differs from expected: $resolvedCanonical"
}
Remove-Item -LiteralPath $resolvedCanonical -Recurse -Force
$exeB = Invoke-CleanBuild $buildCanonical
$hashB = (Get-FileHash -LiteralPath $exeB -Algorithm SHA256).Hash
if ($hashA -ne $hashB) { throw "Double Clean Release is not reproducible: $hashA != $hashB" }
if ((Get-FileHash -LiteralPath $firstArtifact -Algorithm SHA256).Hash -ne $hashA) {
    throw 'Archived first clean Release changed unexpectedly'
}

$desktopUpExe = Join-Path $payload 'DesktopUp.exe'
Copy-Item -LiteralPath $exeB -Destination $desktopUpExe
& $windeployqt --release --no-translations --qmldir (Join-Path $scriptRoot 'qml') --dir $payload $desktopUpExe
if ($LASTEXITCODE -ne 0) { throw 'windeployqt failed for R4-D4 payload' }

$smokeLog = Join-Path $runRoot 'r2-regression.jsonl'
$smoke = Start-Process -FilePath $desktopUpExe -ArgumentList @('--run-r2-fixture','--duration-ms','250','--log',$smokeLog) -WorkingDirectory $payload -WindowStyle Hidden -Wait -PassThru
if ($smoke.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $smokeLog)) {
    throw "Deployed R2 regression failed with exit $($smoke.ExitCode)"
}

$filesBeforeManifest = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        relativePath = [IO.Path]::GetRelativePath($payload, $_.FullName).Replace('\\','/')
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
$deploymentManifest = [ordered]@{
    schema = 'desktopup-ts-c01-r4d4-deployment-manifest-v1'
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
    candidateId = 'TS-C01'
    buildId = $buildId
    qtVersion = '6.8.3'
    architecture = 'x64'
    installScope = 'currentUser'
    defaultInstallRoot = '%LOCALAPPDATA%\Programs\DesktopUp'
    installDirectorySelectable = $true
    formalInstallQualification = 'adjacent-owner-marker-and-HKCU-install-location'
    nonEmptyInstallDirectoryPolicy = 'allow-when-deployment-paths-do-not-collide'
    candidateDataRoot = '%LOCALAPPDATA%\DesktopUp\Candidate\TS-C01'
    autostartDefault = 'OFF'
    autostartValueName = 'DesktopUp'
    autostartArgument = '--autostart'
    productRuntimeImplemented = $false
    candidateLifecycleProbe = $true
    executableSha256 = $hashB
    files = $filesBeforeManifest
}
$deploymentManifestPath = Join-Path $resources 'deployment-manifest.json'
$deploymentManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $deploymentManifestPath -Encoding utf8

$installGuardLines = [Collections.Generic.List[string]]::new()
foreach ($file in @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName).Replace('/', '\')
    if ($relative.Contains('"') -or $relative.Contains('$')) {
        throw "Unsafe NSIS install guard path: $relative"
    }
    $installGuardLines.Add('IfFileExists "$INSTDIR\' + $relative + '" install_target_collision')
}
$installGuardLines.Add('IfFileExists "$INSTDIR\resources\r4d4-formal-install.flag" install_target_collision')
$installGuardLines.Add('IfFileExists "$INSTDIR\uninstall.exe" install_target_collision')
$installGuardLines.Add('Goto install_target_safe')
$installGuardLines.Add('install_target_collision:')
$installGuardLines.Add('MessageBox MB_OK|MB_ICONEXCLAMATION "所选目录中已有与 DesktopUp 部署文件同名的内容。请更换目录，或先移走冲突文件。"')
$installGuardLines.Add('Abort')
$installGuardLines.Add('install_target_safe:')
[IO.File]::WriteAllLines($installGuardInclude, $installGuardLines, [Text.UTF8Encoding]::new($true))

$uninstallLines = [Collections.Generic.List[string]]::new()
foreach ($file in @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName).Replace('/', '\')
    if ($relative.Contains('"') -or $relative.Contains('$')) {
        throw "Unsafe NSIS uninstall file path: $relative"
    }
    $uninstallLines.Add('Delete "$INSTDIR\' + $relative + '"')
}
$uninstallLines.Add('Delete "$INSTDIR\resources\r4d4-formal-install.flag"')
$uninstallLines.Add('Delete "$INSTDIR\uninstall.exe"')
foreach ($directory in @(Get-ChildItem -LiteralPath $payload -Recurse -Directory |
        Sort-Object { $_.FullName.Length } -Descending)) {
    $relative = [IO.Path]::GetRelativePath($payload, $directory.FullName).Replace('/', '\')
    if ($relative.Contains('"') -or $relative.Contains('$')) {
        throw "Unsafe NSIS uninstall directory path: $relative"
    }
    $uninstallLines.Add('RMDir "$INSTDIR\' + $relative + '"')
}
$uninstallLines.Add('RMDir "$INSTDIR"')
[IO.File]::WriteAllLines($uninstallInclude, $uninstallLines, [Text.UTF8Encoding]::new($false))

& $MakensisPath "/DPAYLOAD_ROOT=$payload" "/DOUTPUT_FILE=$installerCandidate" "/DINSTALL_GUARD_INCLUDE=$installGuardInclude" "/DUNINSTALL_INCLUDE=$uninstallInclude" $nsi
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerCandidate -PathType Leaf)) {
    throw 'NSIS failed to build DesktopUpSetup.exe'
}

function Get-DesktopUpRunValue {
    try {
        return [string](Get-ItemPropertyValue -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name DesktopUp -ErrorAction Stop)
    } catch {
        return $null
    }
}

$desktopUpProcessIdsBefore = @(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$runValueBefore = Get-DesktopUpRunValue
$uninstallBefore = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp' -ErrorAction SilentlyContinue
$sandboxInstall = Join-Path $runRoot 'sandbox-install'
$sandboxSentinel = Join-Path $sandboxInstall 'keep-user-file.txt'
New-Item -ItemType Directory -Path $sandboxInstall | Out-Null
Set-Content -LiteralPath $sandboxSentinel -Value 'R4-D4 non-empty directory preservation sentinel' -Encoding utf8
$installProcess = Start-Process -FilePath $installerCandidate -ArgumentList @('/S', '/R4D4PACKAGEPROBE=1', "/D=$sandboxInstall") -Wait -PassThru
if ($installProcess.ExitCode -ne 0) { throw "Sandbox installer failed: $($installProcess.ExitCode)" }
foreach ($entry in @('DesktopUp.exe','uninstall.exe','resources')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sandboxInstall $entry))) {
        throw "Sandbox installation missing required entry: $entry"
    }
}
$portable = Start-Process -FilePath (Join-Path $sandboxInstall 'DesktopUp.exe') -WorkingDirectory $sandboxInstall -WindowStyle Hidden -Wait -PassThru
if ($portable.ExitCode -ne 3) { throw "Non-formal install path was not rejected: $($portable.ExitCode)" }
$uninstallProcess = Start-Process -FilePath (Join-Path $sandboxInstall 'uninstall.exe') -ArgumentList '/S' -Wait -PassThru
if ($uninstallProcess.ExitCode -ne 0) { throw "Sandbox uninstaller failed: $($uninstallProcess.ExitCode)" }
$deadline = [DateTime]::UtcNow.AddSeconds(8)
while ((Test-Path -LiteralPath (Join-Path $sandboxInstall 'DesktopUp.exe')) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
foreach ($entry in @('DesktopUp.exe','uninstall.exe','resources')) {
    if (Test-Path -LiteralPath (Join-Path $sandboxInstall $entry)) {
        throw "Sandbox uninstall left deployed entry: $entry"
    }
}
if (-not (Test-Path -LiteralPath $sandboxSentinel -PathType Leaf)) {
    throw 'Sandbox uninstall deleted the non-deployment sentinel file'
}
$resolvedSandbox = (Resolve-Path -LiteralPath $sandboxInstall).Path.TrimEnd('\')
$expectedSandbox = [IO.Path]::GetFullPath($sandboxInstall).TrimEnd('\')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals($resolvedSandbox, $expectedSandbox) -or
    -not $resolvedSandbox.StartsWith($runRootFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected sandbox path: $resolvedSandbox"
}
Remove-Item -LiteralPath $sandboxSentinel -Force
Remove-Item -LiteralPath $resolvedSandbox
$runValueAfter = Get-DesktopUpRunValue
$uninstallAfter = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp' -ErrorAction SilentlyContinue
if ($runValueBefore -ne $runValueAfter) { throw 'Sandbox package probe changed the DesktopUp Run value' }
if ([bool]$uninstallBefore -ne [bool]$uninstallAfter) { throw 'Sandbox package probe changed DesktopUp uninstall registration' }
$desktopUpProcessIdsAfter = @(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$newDesktopUpProcessIds = @($desktopUpProcessIdsAfter | Where-Object { $_ -notin $desktopUpProcessIdsBefore })
if ($newDesktopUpProcessIds.Count -ne 0) {
    throw "Sandbox package probe left new DesktopUp process IDs: $($newDesktopUpProcessIds -join ',')"
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $installerCandidate -Destination $stableInstallerPath -Force
$installerHash = (Get-FileHash -LiteralPath $stableInstallerPath -Algorithm SHA256).Hash
$summary = [ordered]@{
    schema = 'desktopup-ts-c01-r4d4-preflight-v1'
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
    result = 'PASS'
    p0Result = 'NOT_RUN'
    d3Disposition = 'USER_ACCEPTED_WITH_WORKING_SET_EXCEPTION'
    recordedWorkingSetMiB = 136.332
    originalWorkingSetTargetMiB = 50
    runRoot = $runRoot
    buildId = $buildId
    doubleCleanRelease = 'PASS'
    executableSha256 = $hashB
    dependencyFileCount = @(Get-ChildItem -LiteralPath $payload -Recurse -File).Count
    deployedRegression = 'PASS'
    installerName = 'DesktopUpSetup.exe'
    installerSha256 = $installerHash
    installerSize = (Get-Item -LiteralPath $stableInstallerPath).Length
    requestExecutionLevel = 'user'
    defaultInstallRoot = '%LOCALAPPDATA%\Programs\DesktopUp'
    installDirectorySelectable = $true
    formalInstallQualification = 'adjacent-owner-marker-and-HKCU-install-location'
    nonEmptyInstallDirectoryPolicy = 'allow-when-deployment-paths-do-not-collide'
    candidateDataRoot = '%LOCALAPPDATA%\DesktopUp\Candidate\TS-C01'
    autostartDefault = 'OFF'
    sandboxInstallUninstall = 'PASS'
    sandboxNonEmptyDirectoryPreserved = 'PASS'
    sandboxRegistryUnchanged = 'PASS'
    formalInstallLifecycle = 'NOT_RUN'
    deviceRestart = 'NOT_RUN'
    fullProductStateStore = 'NOT_IMPLEMENTED'
    fullProductTrayAndControlCenter = 'NOT_IMPLEMENTED'
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Copy-Item -LiteralPath $summaryPath -Destination $stableSummaryPath -Force
Write-Output "R4D4_RESULT=PASS"
Write-Output "R4D4_P0_RESULT=NOT_RUN"
Write-Output "R4D4_INSTALLER=$stableInstallerPath"
Write-Output "R4D4_INSTALLER_SHA256=$installerHash"
Write-Output "R4D4_RUN_ROOT=$runRoot"
