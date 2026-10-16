#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$ModuleFixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedAssemblySha256,
    [ValidateSet('Lamp','Rain')][string]$Module = 'Lamp'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$moduleRoot = Split-Path -Parent $PSScriptRoot
$moduleApp = (Resolve-Path -LiteralPath $AppDirectory).Path
$moduleFixture = (Resolve-Path -LiteralPath $ModuleFixtureDirectory).Path
$moduleOutput = [IO.Path]::GetFullPath($OutputRoot)
$moduleNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3'))
if (!( $moduleOutput.StartsWith((Join-Path $moduleRoot 'out') + '\', [StringComparison]::OrdinalIgnoreCase)) -or
    $moduleOutput.StartsWith($moduleNormal, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $moduleOutput)) {
    throw 'Use a NEW project out/ directory; existing artifacts and user data are never overwritten.'
}
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first. No process was stopped.' }
if ((Get-FileHash -LiteralPath (Join-Path $moduleApp 'DesktopUp.dll')).Hash -ine $ExpectedAssemblySha256) { throw 'Candidate hash mismatch.' }
New-Item -ItemType Directory -Path $moduleOutput | Out-Null
$moduleSource = Join-Path $moduleOutput ('without-' + $Module.ToLowerInvariant() + '-source')
New-Item -ItemType Directory -Path $moduleSource | Out-Null
$moduleSourceFiles = @(& git -C $moduleRoot ls-files -c -o --exclude-standard) | Sort-Object -Unique
$moduleManifest = foreach ($moduleRelative in $moduleSourceFiles) {
    if ($moduleRelative -match '^(\.git/|\.workbuddy/|out/)' -or $moduleRelative.StartsWith('Modules/' + $Module + '/', [StringComparison]::OrdinalIgnoreCase) -or
        $moduleRelative -match '(^|/)(bin|obj)/') { continue }
    $moduleOriginal = Join-Path $moduleRoot $moduleRelative
    if (!(Test-Path -LiteralPath $moduleOriginal -PathType Leaf)) { continue }
    $moduleTarget = Join-Path $moduleSource $moduleRelative
    New-Item -ItemType Directory -Path (Split-Path -Parent $moduleTarget) -Force | Out-Null
    Copy-Item -LiteralPath $moduleOriginal -Destination $moduleTarget
    $moduleHash = (Get-FileHash -LiteralPath $moduleOriginal).Hash
    if ((Get-FileHash -LiteralPath $moduleTarget).Hash -cne $moduleHash) { throw 'Source snapshot mismatch.' }
    [pscustomobject]@{ Path = $moduleRelative; SHA256 = $moduleHash }
}
if (Test-Path -LiteralPath (Join-Path $moduleSource ('Modules/' + $Module))) { throw 'Selected module was not physically excluded.' }
$moduleManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $moduleOutput ('without-' + $Module.ToLowerInvariant() + '-source.json')) -Encoding utf8
$moduleRemovedApp = Join-Path $moduleOutput ('without-' + $Module.ToLowerInvariant() + '-app')
$moduleRemovedFixture = Join-Path $moduleOutput ('without-' + $Module.ToLowerInvariant() + '-fixture')
& dotnet build (Join-Path $moduleSource 'src\DesktopUp.App\DesktopUp.App.csproj') -c Release -p:Platform=x64 -p:DesktopUpReviewTools=true -o ($moduleRemovedApp + '\')
if ($LASTEXITCODE -ne 0) { throw 'Physical-removal App build failed; source/evidence preserved.' }
& dotnet build (Join-Path $moduleSource 'tests\DesktopUp.SceneModuleFixture\DesktopUp.SceneModuleFixture.csproj') -c Release -o ($moduleRemovedFixture + '\')
if ($LASTEXITCODE -ne 0) { throw 'Physical-removal fixture build failed.' }
$moduleData = Join-Path $moduleOutput 'data'
$moduleFullTool = Join-Path $moduleFixture 'DesktopUp.SceneModuleFixture.exe'
$moduleRemovedTool = Join-Path $moduleRemovedFixture 'DesktopUp.SceneModuleFixture.exe'
$moduleResults = [Collections.Generic.List[object]]::new()
function Invoke-ModuleFixture([string]$tool, [string]$operation, [string]$stage) {
    $moduleText = @(& $tool $operation $moduleData $Module.ToLowerInvariant())
    if ($LASTEXITCODE -ne 0) { throw "Fixture $stage failed." }
    $moduleObservation = ($moduleText -join [Environment]::NewLine) | ConvertFrom-Json
    if (!$moduleObservation.passed) { throw "Fixture $stage refused." }
    $moduleObservation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $moduleOutput ($stage + '.json')) -Encoding utf8
    $moduleResults.Add([pscustomobject]@{ Stage = $stage; Observation = $moduleObservation })
}
function Invoke-ModuleApp([string]$directory, [string]$stage, [bool]$available) {
    $moduleProcess = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $directory 'DesktopUp.exe') -DataRoot $moduleData -WindowStyle Hidden -Arguments '--scene-module-probe --exit-after-ms=35000'
    if (!$moduleProcess.WaitForExit(45000)) { throw "Module App timed out; PID $($moduleProcess.Id) preserved for normal exit." }
    $moduleEvents = @(Get-Content -LiteralPath (Join-Path $moduleData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object processId -eq $moduleProcess.Id)
    $moduleObserved = @($moduleEvents | Where-Object eventName -eq 'scene.module_observed')
    $moduleStopped = @($moduleEvents | Where-Object eventName -eq 'application.stopped')
    if ($moduleProcess.ExitCode -ne 0 -or $moduleObserved.Count -ne 1 -or !$moduleObserved[0].data.passed -or
        $moduleObserved[0].data.moduleAvailable -ne $available -or $moduleStopped.Count -ne 1 -or $moduleStopped[0].data.activeResources -ne 0 -or
        @($moduleEvents | Where-Object level -in @('error','critical','fatal')).Count -ne 0) {
        throw "Module App $stage failed; logs and screenshots preserved."
    }
    $moduleStage = Join-Path $moduleOutput $stage
    New-Item -ItemType Directory -Path $moduleStage | Out-Null
    Get-ChildItem -LiteralPath $moduleData -Filter 'scene-module-*.png' -File | Copy-Item -Destination $moduleStage
    $moduleEvents | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $moduleStage 'events.json') -Encoding utf8
    $moduleResults.Add([pscustomobject]@{ Stage = $stage; ProcessId = $moduleProcess.Id; AppSHA256 = (Get-FileHash (Join-Path $directory 'DesktopUp.dll')).Hash; ActiveResources = 0 })
}
Invoke-ModuleFixture $moduleFullTool 'seed' 'seed'
Invoke-ModuleApp $moduleApp 'before-removal' $true
Invoke-ModuleApp $moduleRemovedApp 'after-removal' $false
Invoke-ModuleFixture $moduleRemovedTool 'backup' 'backup-without-module'
Invoke-ModuleFixture $moduleRemovedTool 'restore' 'restore-without-module'
Invoke-ModuleApp $moduleRemovedApp 'after-backup-restore' $false
Invoke-ModuleFixture $moduleFullTool 'check' 'restore-module-check'
Invoke-ModuleApp $moduleApp 'after-module-restored' $true
# R7: data-only portable export/import also works with the physical module absent.
# These console checks do not certify picker interaction or desktop input.
$modulePortableData = Join-Path $moduleOutput 'portable-data'
$modulePortableFull = Join-Path $moduleOutput 'full.desktopup.json'
$modulePortableMissing = Join-Path $moduleOutput 'missing.desktopup.json'
foreach ($moduleRun in @(
    @{Tool=$moduleFullTool; Args=@('portable-export',$moduleData,$modulePortableFull); Stage='portable-export-full'},
    @{Tool=$moduleRemovedTool; Args=@('portable-export',$moduleData,$modulePortableMissing); Stage='portable-export-missing'},
    @{Tool=$moduleRemovedTool; Args=@('portable-import',$modulePortableData,$modulePortableFull); Stage='portable-import-missing'},
    @{Tool=$moduleRemovedTool; Args=@('portable-check',$modulePortableData); Stage='portable-restart-missing'},
    @{Tool=$moduleFullTool; Args=@('portable-check',$modulePortableData); Stage='portable-module-restored'}
)) {
    $modulePortableArgs = $moduleRun.Args
    $moduleText = @(& $moduleRun.Tool @modulePortableArgs)
    if ($LASTEXITCODE -ne 0) { throw "R7 module portable failure: $($moduleRun.Stage)" }
    $moduleObservation = ($moduleText -join [Environment]::NewLine) | ConvertFrom-Json
    if (!$moduleObservation.passed) { throw 'Missing portable module proof.' }
    $moduleObservation | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $moduleOutput ($moduleRun.Stage + '.json'))
    $moduleResults.Add([pscustomobject]@{Stage=$moduleRun.Stage;Observation=$moduleObservation})
}
if ((Get-FileHash -LiteralPath $modulePortableFull).Hash -cne (Get-FileHash -LiteralPath $modulePortableMissing).Hash) {
    throw 'Missing module changed exported data.'
}
$moduleResults | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $moduleOutput 'results.json') -Encoding utf8
[pscustomobject]@{ Result = 'PASS'; RemovedModule = $Module; PhysicalModuleDirectoryAbsent = $true; SourceFiles = @($moduleManifest).Count; OutputRoot = $moduleOutput; NativeInput = $false }
