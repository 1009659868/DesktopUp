#requires -Version 5.1
# One isolated product candidate; no installation, startup registration or desktop samples.
param([ValidateSet('Open','Prepare','Status')][string]$Action='Open')
$ErrorActionPreference='Stop'
$archRepo=Split-Path -Parent $PSScriptRoot
$archApp=Join-Path $archRepo 'out/architecture-posture/product01/DesktopUp.exe'
$archDll=Join-Path $archRepo 'out/architecture-posture/product01/DesktopUp.dll'
$archFixture=Join-Path $archRepo 'out/architecture-foundation/fixture/DesktopUp.SessionFixture.exe'
$archReview=Join-Path $archRepo 'out/drop-d3/architecture-posture-review01'
$archProfile=Join-Path $archReview 'profile'
$archMarker=Join-Path $archReview '.cross-poster-fixture'
$archReady=Join-Path $archReview '.ready'
$archHash='A440176EE5E58232B254BF87F4BC8B97A44DAF30B4137DF2495728B16F0872CE'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$archApp;CandidateExists=(Test-Path -LiteralPath $archDll);Profile=$archProfile;Prepared=(Test-Path -LiteralPath $archReady)}
    return
}
foreach($path in @($archApp,$archDll,$archFixture,(Join-Path (Split-Path $archApp) 'content-worker/DesktopUp.ContentWorker.exe'))){
    if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing candidate: $path"}
}
if((Get-FileHash -LiteralPath $archDll).Hash -cne $archHash){throw 'Candidate changed; do not reuse this acceptance record.'}
$archApplication=Join-Path (Split-Path $archApp) 'DesktopUp.Application.dll'
if((Get-FileHash -LiteralPath $archApplication).Hash -cne '7DAA66CBF05601F85286C1B1D09FA65E5EF34FDA63350D29F84F9C0FF39BAF0D'){
    throw 'Content heat runtime changed; do not reuse this acceptance record.'
}
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
