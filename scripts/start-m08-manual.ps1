#requires -Version 5.1
param([ValidateSet('basic','chain','constrained','blocked','removal')][string]$Scenario='basic',
 [switch]$CheckOnly, [ValidateRange(0,60000)][int]$ExitAfterMs=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskBuild=& (Join-Path $PSScriptRoot 'Get-M08Build.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskData=Join-Path $taskRepo "out\manual\v3-04-m08-07\$Scenario"
$taskExists=Test-Path -LiteralPath $taskData
if ($taskExists -and !(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)) {
 throw "Incomplete fixture retained for diagnosis: $taskData. Do not overwrite it."
}
if ($CheckOnly) {
 [pscustomobject]@{ Result='BUILD/PATH CHECKED - MANUAL PENDING'; Scenario=$Scenario; DataRoot=$taskData;
  FixtureExists=$taskExists; VerifiedArtifacts=$taskBuild.VerifiedArtifacts; App=$taskBuild.App; Launched=$false }; return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally before changing scenarios.' }
if (!$taskExists) {
 $taskMode=switch($Scenario) {
  'chain' { '--spatial-chain-r2' }
  'constrained' { '--r2-constrained' }
  'blocked' { '--r2-blocked' }
  default { '--measure-expanded' }
 }
 & $taskBuild.Fixture $taskData 3 $taskMode | Out-Null
 if ($LASTEXITCODE -ne 0) { throw 'Manual fixture creation failed.' }
}
# Normal product behavior only: no smoke commands, pointer injection or automatic deletion.
$taskArguments='--show-control-center --animation-config="' + $taskBuild.AnimationConfig + '"'
if ($ExitAfterMs -gt 0) { $taskArguments += " --exit-after-ms=$ExitAfterMs" }
$taskProbe=Start-DesktopUpFixtureProcess -ExecutablePath $taskBuild.App -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{ Result='MANUAL PENDING'; Scenario=$Scenario; ProcessId=$taskProbe.Id; DataRoot=$taskData;
 LogPath=(Join-Path $taskData 'logs\desktopup.jsonl'); AppHash=$taskBuild.AppHash;
 PowerShell=$PSVersionTable.PSVersion.ToString(); AutoExitAfterMs=$ExitAfterMs }
