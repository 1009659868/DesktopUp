#requires -Version 5.1
param([string]$CandidateDirectory,[string]$ReviewDirectory,[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$returnRepo=Split-Path -Parent $PSScriptRoot
if(!$CandidateDirectory){$CandidateDirectory=Join-Path $returnRepo 'out/content-lifecycle/candidate04'}
if(!$ReviewDirectory){$ReviewDirectory=Join-Path $returnRepo 'out/drop-d3/content-return-review01'}
$returnReview=[IO.Path]::GetFullPath($ReviewDirectory).TrimEnd('\')
if(!$returnReview.StartsWith([IO.Path]::GetFullPath((Join-Path $returnRepo 'out/drop-d3')).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Review must stay inside repository/out/drop-d3.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
$returnApp=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
$returnFixture=Join-Path $CandidateDirectory 'fixture/DesktopUp.SessionFixture.exe'
foreach($returnFile in @($returnApp,$returnFixture,(Join-Path $CandidateDirectory 'app/content-worker/DesktopUp.ContentWorker.runtimeconfig.json'))){if(!(Test-Path -LiteralPath $returnFile)){throw "Missing candidate file: $returnFile"}}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$returnTools=Enter-DesktopUpToolchain -Offline
try {
    $returnProfile=Join-Path $returnReview 'profile'
    $returnMarker=Join-Path $returnReview '.content-return-fixture'
    if(!(Test-Path -LiteralPath $returnReview)){
        New-Item -ItemType Directory -Path $returnReview | Out-Null
        [IO.File]::WriteAllText($returnMarker,'owned')
        & $returnFixture $returnProfile --content-return
        if($LASTEXITCODE -ne 0){throw 'Fixture initialization failed; evidence preserved. Use a new review directory.'}
        [IO.File]::WriteAllText((Join-Path $returnReview '.ready'),'owned')
    }
    if(!(Test-Path -LiteralPath (Join-Path $returnReview '.ready')) -or [IO.File]::ReadAllText($returnMarker) -ne 'owned'){throw 'Not a completed owned review fixture.'}
    if($PrepareOnly){[pscustomobject]@{Prepared=$true;Profile=$returnProfile;Samples=$returnReview;Executable=$returnApp};return}
    $returnProcess=Start-DesktopUpFixtureProcess -ExecutablePath $returnApp -DataRoot $returnProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$returnProcess.Id;Profile=$returnProfile;Samples=$returnReview;Executable=$returnApp}
} finally {Exit-DesktopUpToolchain $returnTools}
