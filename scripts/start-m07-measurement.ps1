#requires -Version 5.1
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskBuild=& (Join-Path $PSScriptRoot 'Get-M07StableBuild.ps1')
if ($CheckOnly) { [pscustomobject]@{ Result='PREFLIGHT PASS'; VerifiedBinaries=$taskBuild.VerifiedBinaries; App=$taskBuild.App; Fixture=$taskBuild.Fixture; Launched=$false }; return }
$taskOutput=Join-Path $taskRepo ('out\verify\v3-04-m07-closeout\resource-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
& (Join-Path $PSScriptRoot 'measure-m07.ps1') -ExecutablePath $taskBuild.App -FixtureExecutablePath $taskBuild.Fixture -AnimationConfig $taskBuild.AnimationConfig -OutputRoot $taskOutput -Repeats 2
