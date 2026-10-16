#requires -Version 5.1
# One isolated product candidate; no installation, startup registration or desktop samples.
param([ValidateSet('Open','Prepare','Status')][string]$Action='Open')
$ErrorActionPreference='Stop'
$archRepo=Split-Path -Parent $PSScriptRoot
$archApp=Join-Path $archRepo 'out/architecture-foundation/product02/DesktopUp.exe'
$archDll=Join-Path $archRepo 'out/architecture-foundation/product02/DesktopUp.dll'
$archFixture=Join-Path $archRepo 'out/architecture-foundation/fixture/DesktopUp.SessionFixture.exe'
$archReview=Join-Path $archRepo 'out/drop-d3/architecture-review01'
$archProfile=Join-Path $archReview 'profile'
$archMarker=Join-Path $archReview '.cross-poster-fixture'
$archReady=Join-Path $archReview '.ready'
$archHash='C2D6BDD727FF3AC170FE5B42839FCCE094F4F826011C931D9EA9C4FA6878C64B'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$archApp;CandidateExists=(Test-Path -LiteralPath $archDll);Profile=$archProfile;Prepared=(Test-Path -LiteralPath $archReady)}
    return
}
foreach($path in @($archApp,$archDll,$archFixture,(Join-Path (Split-Path $archApp) 'content-worker/DesktopUp.ContentWorker.exe'))){
    if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing candidate: $path"}
}
if((Get-FileHash -LiteralPath $archDll).Hash -cne $archHash){throw 'Candidate changed; do not reuse this acceptance record.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit the running DesktopUp normally first. No process stopped.'}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$archTools=Enter-DesktopUpToolchain -Offline
try {
    if(!(Test-Path -LiteralPath $archReview)){
        New-Item -ItemType Directory -Path $archReview | Out-Null
        [IO.File]::WriteAllText($archMarker,'owned')
        # Reuse the accepted fixture: real files remain entirely in project/out.
        & $archFixture $archProfile --cross-poster | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Fixture creation failed; partial evidence preserved, no automatic overwrite.'}
        [IO.File]::WriteAllText($archReady,'owned')
    }
    if(!(Test-Path -LiteralPath $archReady) -or !(Test-Path -LiteralPath $archMarker) -or
        [IO.File]::ReadAllText($archMarker) -ne 'owned'){throw 'Not a completed owned review. Nothing overwritten.'}
    if($Action -eq 'Prepare'){
        [pscustomobject]@{Prepared=$true;Profile=$archProfile;Samples=$archReview;Executable=$archApp}
        return
    }
    $archProcess=Start-DesktopUpFixtureProcess -ExecutablePath $archApp -DataRoot $archProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$archProcess.Id;Profile=$archProfile;Samples=$archReview;Executable=$archApp;ReviewExecutors=$false}
} finally {Exit-DesktopUpToolchain $archTools}
