#requires -Version 5.1
param(
 [Parameter(Mandatory)][string]$AppDirectory,
 [Parameter(Mandatory)][string]$FixtureDirectory,
 [Parameter(Mandatory)][string]$EvidenceDirectory,
 [string[]]$Cases=@('verify','cancel','close','early-close','exit','zero','restart')
)
$ErrorActionPreference='Stop'
$storageRepo=Split-Path -Parent $PSScriptRoot
$storageEvidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$storageAllowed=[IO.Path]::GetFullPath((Join-Path $storageRepo 'out/drop-d2'))+'\'
if(!$storageEvidence.StartsWith($storageAllowed,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $storageEvidence)) {throw 'New isolated out/drop-d2 directory required.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. No process stopped.'}
New-Item -ItemType Directory -Path $storageEvidence | Out-Null
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$storageResults=@()
foreach($storageCase in $Cases){
 if($storageCase -notin @('verify','cancel','close','early-close','exit','zero','restart','seed')){throw 'Unknown case.'}
 $storageCaseRoot=Join-Path $storageEvidence $(if($storageCase -eq 'restart'){'verify'}else{$storageCase})
 $storageProfile=Join-Path $storageCaseRoot 'profile'
 if($storageCase -ne 'restart'){
  New-Item -ItemType Directory -Path $storageCaseRoot | Out-Null
  Set-Content -LiteralPath (Join-Path $storageCaseRoot '.storage-settings-fixture') -Value 'owned' -NoNewline -Encoding ascii
  if($storageCase -eq 'zero'){& (Join-Path $FixtureDirectory 'DesktopUp.SessionFixture.exe') $storageProfile 0 | Out-Null}
  else {& (Join-Path $FixtureDirectory 'DesktopUp.SessionFixture.exe') $storageProfile $(if($storageCase -eq 'seed'){3}else{1}) --measure-expanded | Out-Null}
  if($LASTEXITCODE -ne 0){throw 'Fixture creation failed.'}
 }
 $storageLog=Join-Path $storageProfile 'logs/desktopup.jsonl'
 $storageBefore=if(Test-Path -LiteralPath $storageLog){@(Get-Content -LiteralPath $storageLog).Count}else{0}
 $storageApp=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $AppDirectory 'DesktopUp.exe') -DataRoot $storageProfile -Arguments "--storage-settings-probe --storage-case=$storageCase --show-control-center" -WindowStyle Hidden
 $storageDeadline=[DateTime]::UtcNow.AddSeconds(60)
 while(!$storageApp.WaitForExit(500)){if([DateTime]::UtcNow -gt $storageDeadline){throw "Storage gate timed out; PID $($storageApp.Id) preserved, no force kill. Evidence: $storageProfile"}}
 $storageApp.Refresh()
 $storageEvents=@(Get-Content -LiteralPath $storageLog | Select-Object -Skip $storageBefore | ForEach-Object {$_ | ConvertFrom-Json})
 $storageErrors=@($storageEvents | Where-Object {$_.level -in @('error','critical','fatal')})
 $storageStopped=@($storageEvents | Where-Object eventName -eq 'application.stopped')
 $storagePassed=@($storageEvents | Where-Object eventName -eq $(if($storageCase -eq 'exit'){'contract.storage_settings_exit_armed'}else{'contract.storage_settings_passed'}))
 if($storageApp.ExitCode -ne 0 -or $storageErrors.Count -or $storageStopped.Count -ne 1 -or $storageStopped[0].data.activeResources -ne 0 -or $storagePassed.Count -ne 1){throw "Storage gate failed: $storageCase. Evidence: $storageProfile"}
 $storageResult=[pscustomobject]@{Case=$storageCase;Result='PASS';PID=$storageApp.Id;ExitCode=$storageApp.ExitCode;ActiveResourcesAfterExit=$storageStopped[0].data.activeResources;NativeMouse=$false;DataRoot=$storageProfile}
 $storageResults+=$storageResult;$storageResult
}
$storageResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $storageEvidence 'results.json') -Encoding UTF8
