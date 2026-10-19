#requires -Version 7
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$reviewRepo=Split-Path -Parent $PSScriptRoot
$reviewEvidence=Join-Path $reviewRepo 'out/transfer-adoption'
$reviewProof=Get-Content -LiteralPath (Join-Path $reviewEvidence 'proof.json') -Raw | ConvertFrom-Json
if($reviewProof.Result -ne 'LIMITED_AUTO_PASS' -or $reviewProof.NativeDragEnabled){throw 'Expected limited isolated proof with native drag disabled.'}
$reviewPackage=Join-Path $reviewEvidence $reviewProof.Candidate
foreach($reviewArtifact in $reviewProof.Artifacts){
    $reviewPath=[IO.Path]::GetFullPath((Join-Path $reviewPackage $reviewArtifact.Path))
    if(!$reviewPath.StartsWith($reviewPackage+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $reviewPath).Hash -ne $reviewArtifact.SHA256){throw 'Candidate mismatch; preserve this package.'}
}
foreach($reviewSource in $reviewProof.Sources){
    $reviewPath=[IO.Path]::GetFullPath((Join-Path $reviewRepo $reviewSource.Path))
    if(!$reviewPath.StartsWith($reviewRepo+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $reviewPath).Hash -ne $reviewSource.SHA256){throw 'Source changed since this candidate was verified.'}
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Please exit current DesktopUp normally first; nothing was stopped.'}
$reviewRoot=Join-Path $reviewEvidence $reviewProof.Review
if(!$reviewRoot.StartsWith($reviewEvidence+'\',[StringComparison]::OrdinalIgnoreCase) -or
    (Get-Content -LiteralPath (Join-Path $reviewRoot '.transfer-fixture-owner') -Raw) -ne 'owned'){throw 'Owned review fixture required.'}
$reviewApp=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $reviewPackage 'app/DesktopUp.exe') -DataRoot (Join-Path $reviewRoot 'profile') -Arguments '--show-control-center' -WindowStyle Hidden
[pscustomobject]@{ProcessId=$reviewApp.Id;DataRoot=(Join-Path $reviewRoot 'profile');Scope='Owned files only; native drag disabled'}
