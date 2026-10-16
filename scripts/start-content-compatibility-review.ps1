#requires -Version 5.1
param([ValidateSet('Open','Folder','Status')][string]$Action='Open',[switch]$PrepareOnly,
      [string]$CandidateDirectory,[string]$ReviewDirectory)
$ErrorActionPreference='Stop'
$compatRepo=Split-Path -Parent $PSScriptRoot
if(!$CandidateDirectory){$CandidateDirectory=Join-Path $compatRepo 'out/content-compatibility/candidate02'}
if(!$ReviewDirectory){$ReviewDirectory=Join-Path $compatRepo 'out/drop-d3/content-compatibility-review01'}
$compatReview=[IO.Path]::GetFullPath($ReviewDirectory).TrimEnd('\')
$compatAllowed=[IO.Path]::GetFullPath((Join-Path $compatRepo 'out/drop-d3')).TrimEnd('\')+'\'
if(!$compatReview.StartsWith($compatAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Review must stay inside repository/out/drop-d3.'}
$compatProfile=Join-Path $compatReview 'profile'
$compatReady=Join-Path $compatReview '.ready'
$compatMarker=Join-Path $compatReview '.compatibility-fixture'
$compatApp=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
$compatFixture=Join-Path $CandidateDirectory 'fixture/DesktopUp.SessionFixture.exe'
if($Action -eq 'Status'){
    [pscustomobject]@{Prepared=(Test-Path -LiteralPath $compatReady);Profile=$compatProfile;Samples=$compatReview;Executable=$compatApp}
    return
}
if($Action -eq 'Folder'){
    if(!(Test-Path -LiteralPath $compatMarker)){throw 'Prepare the owned review first.'}
    Start-Process explorer.exe -ArgumentList ('"'+$compatReview+'"')
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
foreach($compatFile in @($compatApp,$compatFixture,(Join-Path $CandidateDirectory 'app/content-worker/DesktopUp.ContentWorker.runtimeconfig.json'))){
    if(!(Test-Path -LiteralPath $compatFile)){throw "Missing candidate file: $compatFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$compatTools=Enter-DesktopUpToolchain -Offline
try {
    if(!(Test-Path -LiteralPath $compatReview)){
        New-Item -ItemType Directory -Path $compatReview | Out-Null
        [IO.File]::WriteAllText((Join-Path $compatReview '.cross-poster-fixture'),'owned')
        & $compatFixture $compatProfile --content-compatibility
        if($LASTEXITCODE -ne 0){throw 'Fixture initialization failed; preserve evidence and use a new directory.'}
        [IO.File]::WriteAllText($compatReady,'owned')
    }
    if(!(Test-Path -LiteralPath $compatReady) -or !(Test-Path -LiteralPath $compatMarker) -or
        [IO.File]::ReadAllText($compatMarker) -ne 'owned'){throw 'Not a completed owned compatibility fixture.'}
    if($PrepareOnly){
        [pscustomobject]@{Prepared=$true;Profile=$compatProfile;Samples=$compatReview;Executable=$compatApp}
        return
    }
    $compatProcess=Start-DesktopUpFixtureProcess -ExecutablePath $compatApp -DataRoot $compatProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$compatProcess.Id;Profile=$compatProfile;Samples=$compatReview;Executable=$compatApp}
} finally {Exit-DesktopUpToolchain $compatTools}
