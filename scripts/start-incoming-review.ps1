#requires -Version 5.1
param([switch]$PrepareOnly,[switch]$ComponentRevision,[switch]$FullFrameRevision,[switch]$ManagedSyncRevision,[switch]$CombinedD5,[switch]$NativeShellExit)
$ErrorActionPreference='Stop'
$incomingRepo=Split-Path -Parent $PSScriptRoot
if(@($ComponentRevision,$FullFrameRevision,$ManagedSyncRevision,$CombinedD5,$NativeShellExit | Where-Object {$_}).Count -gt 1){throw 'Choose one candidate revision.'}
$incomingManifestFile=if($NativeShellExit){'out/d4-native/candidate.json'}elseif($CombinedD5){'out/d5/candidate.json'}elseif($ManagedSyncRevision){'out/managed-sync/candidate.json'}elseif($FullFrameRevision){'out/drop-d3/fullframe-candidate.json'}elseif($ComponentRevision){'out/drop-d3/component-candidate.json'}else{'out/drop-d3/candidate.json'}
$incomingManifest=Get-Content -LiteralPath (Join-Path $incomingRepo $incomingManifestFile) -Raw | ConvertFrom-Json
if($incomingManifest.Result -ne 'AUTO_PASS_USER_PENDING' -and !($PrepareOnly -and $ComponentRevision -and $incomingManifest.Result -eq 'AUTO_PASS_NATIVE_REPEAT_PENDING')){throw 'Verified D3 candidate required. Component revision still needs its final native repeat check.'}
foreach($incomingArtifact in $incomingManifest.Artifacts){
 $incomingPath=[IO.Path]::GetFullPath((Join-Path $incomingRepo $incomingArtifact.Path))
 if(!$incomingPath.StartsWith($incomingRepo+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $incomingPath).Hash -ne $incomingArtifact.Sha256){throw 'Candidate changed; preserve old evidence and build a new candidate.'}
}
$incomingReview=Join-Path $incomingRepo $(if($NativeShellExit){'out/d4-native/human01'}elseif($CombinedD5){'out/d5/human01'}elseif($ManagedSyncRevision){'out/managed-sync/human01'}elseif($FullFrameRevision){'out/drop-d3/fullframe-human01'}elseif($ComponentRevision){'out/drop-d3/component-human01'}else{'out/drop-d3/human01'})
$incomingProfile=Join-Path $incomingReview 'profile'
if(!(Test-Path -LiteralPath $incomingReview)){
 New-Item -ItemType Directory -Path $incomingReview | Out-Null
 New-Item -ItemType File -Path (Join-Path $incomingReview '.incoming-human') | Out-Null
 & (Join-Path $incomingRepo $incomingManifest.Fixture) $incomingProfile --content-layout empty | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Empty review profile could not be created.'}
 $incomingSources=Join-Path $incomingReview 'sources'
 New-Item -ItemType Directory -Path $incomingSources | Out-Null
 foreach($incomingName in @('01-blank.txt','02-position.txt','03-cancel.txt','04-collapsed.txt','05-batch-a.txt','06-batch-b.txt')){
  if($NativeShellExit){$incomingName='DesktopUp-D4N-'+$incomingName}elseif($CombinedD5){$incomingName='DesktopUp-D5-'+$incomingName}
  [IO.File]::WriteAllText((Join-Path $incomingSources $incomingName),'D3 disposable review file. Safe to collect in this isolated candidate.')
 }
 $incomingCross=Join-Path ([IO.Path]::GetTempPath()) ('DesktopUp-D3-Human-'+[Guid]::NewGuid().ToString('N'))
 $incomingFolder=if($CombinedD5){'DesktopUp-D5-cross-folder'}else{'cross-folder'}
 New-Item -ItemType Directory -Path (Join-Path $incomingCross ($incomingFolder+'/empty')) | Out-Null
 [IO.File]::WriteAllText((Join-Path $incomingCross ($incomingFolder+'/child.txt')),'Disposable cross-volume test content.')
 [IO.File]::WriteAllText((Join-Path $incomingReview 'cross-source.txt'),$incomingCross)
 if($NativeShellExit){
  New-Item -ItemType Directory -Path (Join-Path $incomingReview 'drop-target') | Out-Null
  $incomingShell=New-Object -ComObject WScript.Shell
  try {
   $incomingShortcut=$incomingShell.CreateShortcut((Join-Path $incomingSources 'DesktopUp-D4N-shortcut.lnk'))
   $incomingShortcut.TargetPath=Join-Path $incomingSources 'DesktopUp-D4N-03-cancel.txt'
   $incomingShortcut.Save()
  } finally {if($incomingShortcut){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($incomingShortcut)};[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($incomingShell)}
 }
}
if(!(Test-Path -LiteralPath (Join-Path $incomingReview '.incoming-human')) -or !(Test-Path -LiteralPath $incomingProfile)){throw 'Owned review profile required; existing data was preserved.'}
$incomingCross=Get-Content -LiteralPath (Join-Path $incomingReview 'cross-source.txt') -Raw
if($PrepareOnly){[pscustomobject]@{Started=$false;Review=$incomingReview;CrossVolumeSource=$incomingCross};return}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. No process stopped.'}
# Reuse the verified read-only environment checker; do not adjust UAC, tokens or UIPI.
$incomingEnvironment=Join-Path $incomingReview ('environment-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $incomingEnvironment | Out-Null
New-Item -ItemType File -Path (Join-Path $incomingEnvironment 'd1-probe-owned.marker') | Out-Null
$incomingCheck=Start-Process -FilePath (Join-Path $incomingRepo $incomingManifest.EnvironmentProbe) -ArgumentList @('--check-environment',('"'+$incomingEnvironment+'"')) -WindowStyle Hidden -PassThru
if(!$incomingCheck.WaitForExit(15000)){throw "Environment check timed out; PID $($incomingCheck.Id) preserved."}
$incomingEnv=Get-Content -LiteralPath (Join-Path $incomingEnvironment 'environment.json') -Raw | ConvertFrom-Json
if($incomingCheck.ExitCode -ne 0 -or !$incomingEnv.Allowed){throw "Explorer environment mismatch: $($incomingEnv.Reason). Do not bypass UIPI."}
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$incomingProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $incomingRepo $incomingManifest.Executable) -DataRoot $incomingProfile -Arguments '--show-control-center' -WindowStyle Hidden
[pscustomobject]@{ProcessId=$incomingProcess.Id;Environment=$incomingEnv.Kind;Profile=$incomingProfile;Sources=(Join-Path $incomingReview 'sources');CrossVolumeSource=$incomingCross;Scope=$(if($NativeShellExit){'D4-N: native Shell egress, continuous drag, no extra move confirmation; stale references confirm only after release. Only use disposable review files.'}elseif($CombinedD5){'D5: blank marquee, desktop egress, external sync and restart. Only use these disposable sources.'}else{'D3 real file moves: use only these disposable review sources. D4 outgoing is not under test.'})}
