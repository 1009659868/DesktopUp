#requires -Version 5.1
param([switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$directRepo=Split-Path -Parent $PSScriptRoot
$directEvidence=Join-Path $directRepo 'out/direct-transfer'
$directProof=Get-Content -LiteralPath (Join-Path $directEvidence 'proof.json') -Raw | ConvertFrom-Json
if($directProof.Result -ne 'AUTO_PASS_USER_PENDING'){throw 'Verified candidate required.'}
$directPackage=Join-Path $directEvidence $directProof.Candidate
foreach($directSource in $directProof.Sources){
    $directPath=[IO.Path]::GetFullPath((Join-Path $directRepo $directSource.Path))
    if(!$directPath.StartsWith($directRepo+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $directPath).Hash -ne $directSource.SHA256){throw 'Sources changed; do not rebind old proof.'}
}
foreach($directArtifact in $directProof.Artifacts){
    $directPath=[IO.Path]::GetFullPath((Join-Path $directPackage $directArtifact.Path))
    if(!$directPath.StartsWith($directPackage+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $directPath).Hash -ne $directArtifact.SHA256){throw 'Candidate changed; preserve old package.'}
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. No process stopped.'}
if(!$PrepareOnly){
    $directIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $directPrincipal=New-Object Security.Principal.WindowsPrincipal($directIdentity)
    if($directPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
        throw 'Explorer drag-in needs the same ordinary integrity level. Run this script in a NON-administrator PowerShell window.'
    }
}
$directProfile=Join-Path $directEvidence 'review01/profile'
if(!(Test-Path -LiteralPath $directProfile)){
    & (Join-Path $directPackage 'fixture/DesktopUp.SessionFixture.exe') $directProfile 3 --measure-expanded
    if($LASTEXITCODE -ne 0){throw 'Isolated fixture creation failed; preserve evidence.'}
}
if($PrepareOnly){
    [pscustomobject]@{Profile=$directProfile;Prepared=$true;Started=$false}
    return
}
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$directApp=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $directPackage 'app/DesktopUp.exe') -DataRoot $directProfile -Arguments '--show-control-center' -WindowStyle Hidden
[pscustomobject]@{ProcessId=$directApp.Id;Profile=$directProfile;Scope='Same-volume desktop files only; use self-created test files, not valuable files.'}
