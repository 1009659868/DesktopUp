#requires -Version 5.1
param([string]$CandidateDirectory,[string]$ReviewDirectory,[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$crossRepo=Split-Path -Parent $PSScriptRoot
if(!$CandidateDirectory){$CandidateDirectory=Join-Path $crossRepo 'out/cross-poster/candidate03'}
if(!$ReviewDirectory){$ReviewDirectory=Join-Path $crossRepo 'out/drop-d3/cross-poster-review01'}
$crossReview=[IO.Path]::GetFullPath($ReviewDirectory).TrimEnd('\')
if(!$crossReview.StartsWith([IO.Path]::GetFullPath((Join-Path $crossRepo 'out/drop-d3')).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Review must stay inside repository/out/drop-d3.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
$crossApp=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
$crossFixture=Join-Path $CandidateDirectory 'fixture/DesktopUp.SessionFixture.exe'
foreach($file in @($crossApp,$crossFixture,(Join-Path $CandidateDirectory 'app/content-worker/DesktopUp.ContentWorker.runtimeconfig.json'))){if(!(Test-Path -LiteralPath $file)){throw "Missing candidate file: $file"}}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$crossTools=Enter-DesktopUpToolchain -Offline
try {
    $crossProfile=Join-Path $crossReview 'profile'
    $crossMarker=Join-Path $crossReview '.cross-poster-fixture'
    if(!(Test-Path -LiteralPath $crossReview)){
        New-Item -ItemType Directory -Path $crossReview | Out-Null
        [IO.File]::WriteAllText($crossMarker,'owned')
        & $crossFixture $crossProfile --cross-poster
        if($LASTEXITCODE -ne 0){throw 'Fixture initialization failed; evidence preserved. Use a new review directory.'}
        [IO.File]::WriteAllText((Join-Path $crossReview '.ready'),'owned')
    }
    if(!(Test-Path -LiteralPath (Join-Path $crossReview '.ready')) -or [IO.File]::ReadAllText($crossMarker) -ne 'owned'){throw 'Not a completed owned review fixture.'}
    if($PrepareOnly){[pscustomobject]@{Prepared=$true;Profile=$crossProfile;Samples=$crossReview;Executable=$crossApp};return}
    $crossProcess=Start-DesktopUpFixtureProcess -ExecutablePath $crossApp -DataRoot $crossProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$crossProcess.Id;Profile=$crossProfile;Samples=$crossReview;Executable=$crossApp}
} finally {Exit-DesktopUpToolchain $crossTools}
