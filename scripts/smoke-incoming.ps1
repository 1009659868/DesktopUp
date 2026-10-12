#requires -Version 5.1
param([Parameter(Mandatory)][string]$AppDirectory,[Parameter(Mandatory)][string]$FixtureDirectory,
 [Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$incomingRepo=Split-Path -Parent $PSScriptRoot
$incomingRoot=[IO.Path]::GetFullPath($EvidenceDirectory)
if(!$incomingRoot.StartsWith([IO.Path]::GetFullPath((Join-Path $incomingRepo 'out/drop-d3'))+'\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $incomingRoot)){throw 'New isolated D3 evidence directory required.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. No process stopped.'}
New-Item -ItemType Directory -Path $incomingRoot | Out-Null
[IO.File]::WriteAllText((Join-Path $incomingRoot '.incoming-fixture'),'owned')
$incomingProfile=Join-Path $incomingRoot 'profile'
& (Join-Path $FixtureDirectory 'DesktopUp.SessionFixture.exe') $incomingProfile --content-layout empty | Out-Null
if($LASTEXITCODE -ne 0){throw 'Empty fixture creation failed.'}
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$incomingResults=@()
foreach($incomingMode in @('first','restart')){
 $incomingLog=Join-Path $incomingProfile 'logs/desktopup.jsonl'
 $incomingBefore=if(Test-Path -LiteralPath $incomingLog){@(Get-Content -LiteralPath $incomingLog).Count}else{0}
 $incomingArgs='--incoming-probe'
 if($incomingMode -eq 'restart'){$incomingArgs+=' --incoming-restart'}
 $incomingProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $AppDirectory 'DesktopUp.exe') -DataRoot $incomingProfile -Arguments $incomingArgs -WindowStyle Hidden
 $incomingDeadline=[DateTime]::UtcNow.AddSeconds(75)
 while(!$incomingProcess.WaitForExit(300)){if([DateTime]::UtcNow -gt $incomingDeadline){throw "Timeout; PID $($incomingProcess.Id) retained. Evidence $incomingRoot"}}
 $incomingProcess.Refresh()
 $incomingEvents=@(Get-Content -LiteralPath $incomingLog | Select-Object -Skip $incomingBefore | ForEach-Object {$_ | ConvertFrom-Json})
 $incomingErrors=@($incomingEvents | Where-Object level -in @('error','critical','fatal'))
 $incomingStopped=@($incomingEvents | Where-Object eventName -eq 'application.stopped')
 if($incomingProcess.ExitCode -ne 0 -or $incomingErrors.Count -or $incomingStopped.Count -ne 1 -or $incomingStopped[0].data.activeResources -ne 0 -or !($incomingEvents | Where-Object eventName -eq 'contract.incoming_passed')){throw "Incoming gate failed ($incomingMode); inspect $incomingLog"}
 $incomingResults+=[pscustomobject]@{Case=$incomingMode;Result='PASS';PID=$incomingProcess.Id;ExitCode=$incomingProcess.ExitCode;ActiveResources=0;NativeMouse=$false;Profile=$incomingProfile}
}
$incomingResults | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $incomingRoot 'results.json') -Encoding UTF8
$incomingResults
