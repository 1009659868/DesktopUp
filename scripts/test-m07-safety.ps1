#requires -Version 5.1
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskBuild=& (Join-Path $PSScriptRoot 'Get-M07StableBuild.ps1')
if ($CheckOnly) { [pscustomobject]@{ Result='PREFLIGHT PASS'; VerifiedBinaries=$taskBuild.VerifiedBinaries; Launched=$false }; return }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally before safety probes.' }
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskRoot=Join-Path $taskRepo ('out\verify\v3-04-m07-closeout\safety-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
$taskResults=[Collections.Generic.List[object]]::new()
function New-SafetyFixture([string]$Name,[int]$Count,[string]$Mode) {
 $taskPath=Join-Path $taskRoot $Name
 $taskParameters=@($taskPath,$Count)
 if ($Mode) { $taskParameters += $Mode }
 & $taskBuild.Fixture @taskParameters | Out-Null
 if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $Name" }
 return $taskPath
}
function Save-SafetyResult([string]$Name,$Result) {
 $taskResults.Add([pscustomobject]@{ Case=$Name; Detail=$Result })
 [ordered]@{ AppHash=$taskBuild.AppHash; PowerShell=$PSVersionTable.PSVersion.ToString(); Scope='programmatic safety, not physical clicks';
  Results=$taskResults.ToArray() } | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath (Join-Path $taskRoot 'results.json') -Encoding utf8
 Write-Output "$Name PASS PID=$($Result.ProcessId)"
}
function Read-SafetyCatalog([string]$DataRoot) {
 if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Unexpected live process.' }
 $taskP=Start-DesktopUpFixtureProcess -ExecutablePath $taskBuild.App -DataRoot $DataRoot -Arguments '--show-control-center --exit-after-ms=6000' -WindowStyle Hidden
 if (!$taskP.WaitForExit(12000)) { throw "No forced termination; inspect PID $($taskP.Id)" }
 $taskRows=@(Get-Content -LiteralPath (Join-Path $DataRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskP.Id)
 $taskLedger=Get-Content -LiteralPath (Join-Path $DataRoot 'state\resource-ledger.json') -Raw | ConvertFrom-Json
 $taskViews=@($taskRows | Where-Object eventName -eq 'control_center.projection_applied')
 if ($taskP.ExitCode -ne 0 -or !$taskLedger.clean -or @($taskLedger.resources).Count -ne 0 -or $taskViews.Count -eq 0 -or
     @($taskRows | Where-Object level -in 'error','critical').Count -ne 0 -or
     @($taskRows | Where-Object eventName -eq 'control_center.created').Count -ne 1 -or
     @($taskRows | Where-Object eventName -eq 'tray.created').Count -ne 1) { throw 'Catalog restart failed.' }
 $taskView=$taskViews[-1].data
 $taskResult=[pscustomobject]@{ ProcessId=$taskP.Id; ExitCode=$taskP.ExitCode; PosterCount=$taskView.PosterCount; PosterIds=@($taskView.PosterIds);
  Revision=$taskView.Revision; ReadyPosters=@($taskRows | Where-Object eventName -eq 'poster_session.ready').Count; ResourcesAfterExit=0 }
 $taskP.Dispose()
 return $taskResult
}
$taskConfirm=New-SafetyFixture 'confirmation' 3 '--multi-instance'
$taskResult=& (Join-Path $PSScriptRoot 'smoke-input-confirmation.ps1') -ExecutablePath $taskBuild.App -DataRoot $taskConfirm -ExitWithDialog
Save-SafetyResult 'confirmation-cancel-close-exit' $taskResult
$taskInitialRevision=$taskResult.StateRevision
$taskResult=Read-SafetyCatalog $taskConfirm
if ($taskResult.PosterCount -ne 3 -or $taskResult.ReadyPosters -ne 3 -or $taskResult.Revision -ne $taskInitialRevision) { throw 'Confirmation exit changed the catalog.' }
Save-SafetyResult 'confirmation-restart' $taskResult
$taskMulti=New-SafetyFixture 'multi' 3 '--multi-instance'
foreach($taskPhase in @('Exercise','Restart')) {
 $taskResult=& (Join-Path $PSScriptRoot 'smoke-multi-instance.ps1') -ExecutablePath $taskBuild.App -DataRoot $taskMulti -Phase $taskPhase
 Save-SafetyResult ("multi-" + $taskPhase) $taskResult
}
$taskBlocked=New-SafetyFixture 'blocked' 3 '--r2-blocked'
$taskResult=& (Join-Path $PSScriptRoot 'smoke-spatial.ps1') -ExecutablePath $taskBuild.App -DataRoot $taskBlocked -ExpectedExpansion InsufficientSpace -AnimationConfig $taskBuild.AnimationConfig
Save-SafetyResult 'fixed-obstacles-no-space' $taskResult
$taskZero=New-SafetyFixture 'zero' 0 ''
$taskZeroRevision=$null
foreach($taskRound in @(1,2)) {
 $taskResult=Read-SafetyCatalog $taskZero
 if ($taskResult.PosterCount -ne 0 -or $taskResult.PosterIds.Count -ne 0 -or $taskResult.ReadyPosters -ne 0) { throw 'Zero catalog resurrected a poster.' }
 if ($null -ne $taskZeroRevision -and $taskResult.Revision -ne $taskZeroRevision) { throw 'Zero restart changed committed revision.' }
 $taskZeroRevision=$taskResult.Revision
 Save-SafetyResult "zero-restart-$taskRound" $taskResult
}
Write-Output "SAFETY AUTO PASS: $taskRoot"
