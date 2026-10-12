#requires -Version 5.1
param([switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$storageRepo=Split-Path -Parent $PSScriptRoot
$storageProof=Get-Content -LiteralPath (Join-Path $storageRepo 'out/drop-d2/proof-settings01.json') -Raw | ConvertFrom-Json
if($storageProof.Result -ne 'AUTO_PASS_USER_PENDING'){throw 'Verified settings candidate required.'}
foreach($storageEntry in @($storageProof.Inputs)+@($storageProof.BuildArtifacts)){
 $storagePath=[IO.Path]::GetFullPath((Join-Path $storageRepo $storageEntry.Path))
 if(!$storagePath.StartsWith($storageRepo+'\',[StringComparison]::OrdinalIgnoreCase) -or
    !(Test-Path -LiteralPath $storagePath) -or (Get-FileHash -LiteralPath $storagePath).Hash -ne $storageEntry.Sha256){throw 'Source/candidate changed. Preserve proof; build a new candidate, do not rebind.'}
}
foreach($storageEvidence in @($storageProof.FullTrx)+@($storageProof.Gates)){
 $storagePath=[IO.Path]::GetFullPath((Join-Path $storageRepo $storageEvidence.Path))
 if(!$storagePath.StartsWith($storageRepo+'\',[StringComparison]::OrdinalIgnoreCase) -or
    (Get-FileHash -LiteralPath $storagePath).Hash -ne $storageEvidence.Sha256){throw 'Verification evidence changed.'}
}
$storageProfile=[IO.Path]::GetFullPath((Join-Path $storageRepo $storageProof.ReviewProfile))
$storageAllowed=[IO.Path]::GetFullPath((Join-Path $storageRepo 'out/drop-d2'))+'\'
if(!$storageProfile.StartsWith($storageAllowed,[StringComparison]::OrdinalIgnoreCase) -or
   !(Test-Path -LiteralPath (Join-Path (Split-Path -Parent $storageProfile) '.storage-settings-fixture'))){throw 'Prepared isolated review profile required.'}
$storageExe=Join-Path $storageRepo ($storageProof.Candidate+'/app/DesktopUp.exe')
if($PrepareOnly){[pscustomobject]@{Prepared=$true;Started=$false;Profile=$storageProfile;Candidate=$storageExe};return}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. No process stopped.'}
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$storageApp=Start-DesktopUpFixtureProcess -ExecutablePath $storageExe -DataRoot $storageProfile -Arguments '--show-control-center' -WindowStyle Hidden
[pscustomobject]@{ProcessId=$storageApp.Id;Profile=$storageProfile;Scope='D2-04 storage settings. Only the seeded test content; native full-area drag remains D3.'}
