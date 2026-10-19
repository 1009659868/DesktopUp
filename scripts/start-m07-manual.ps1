#requires -Version 5.1
param([ValidateSet('interaction','chain','return-sync','removal','blocked')][string]$Scenario='interaction', [switch]$CheckOnly,
 [ValidateRange(0,60000)][int]$ExitAfterMs=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskBuild=& (Join-Path $PSScriptRoot 'Get-M07StableBuild.ps1')
# Versioned, persistent manual fixtures: deleting to zero MUST survive another invocation.
$taskData=Join-Path $taskRepo "out\manual\v3-04-m07-closeout\$Scenario"
$taskExists=Test-Path -LiteralPath $taskData
if ($taskExists -and !(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)) {
 throw "Incomplete manual fixture: $taskData. Preserve it for diagnosis; do not recreate in place."
}
if ($CheckOnly) {
 [pscustomobject]@{ Result='PREFLIGHT PASS'; Scenario=$Scenario; DataRoot=$taskData;
  FixtureExists=$taskExists; VerifiedBinaries=$taskBuild.VerifiedBinaries; App=$taskBuild.App; Launched=$false }
 return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the previous DesktopUp normally; never launch during measurement.' }
if (!$taskExists) {
 $taskMode=switch($Scenario) {
  'chain' { '--spatial-chain-r2' }
  'return-sync' { '--measure-collapsed' }
  'blocked' { '--r2-blocked' }
  default { '--multi-instance' }
 }
 & $taskBuild.Fixture $taskData 3 $taskMode | Out-Null
 if ($LASTEXITCODE -ne 0) { throw "Manual fixture creation failed: $taskData" }
}
$taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
$taskElevated=([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$taskIdentity.Dispose()
$taskArguments='--show-control-center --animation-config="' + $taskBuild.AnimationConfig + '"'
if ($Scenario -eq 'return-sync' -and !$taskExists) { $taskArguments += ' --continuity-arrange' }
if ($ExitAfterMs -gt 0) { $taskArguments += " --exit-after-ms=$ExitAfterMs" }
$taskProbe=Start-DesktopUpFixtureProcess -ExecutablePath $taskBuild.App -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{ Result='MANUAL PENDING'; Scenario=$Scenario; ProcessId=$taskProbe.Id; DataRoot=$taskData;
 LogPath=(Join-Path $taskData 'logs\desktopup.jsonl'); PowerShell=$PSVersionTable.PSVersion.ToString();
 AppHash=$taskBuild.AppHash; Elevated=$taskElevated; EvidenceScope='current-environment'; AutoExitAfterMs=$ExitAfterMs }
