#requires -Version 5.1
param([ValidateSet('interaction','chain','return-sync','removal')][string]$Scenario='interaction', [switch]$CheckOnly,
 [ValidateRange(0,60000)][int]$ExitAfterMs=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskApp=Join-Path $taskRepo 'out\verify\v3-04-m07\app'
$taskData=Join-Path $taskRepo "out\manual\v3-04-m07\$Scenario"
$taskPins=@((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'm07-build.json') -Raw | ConvertFrom-Json).PSObject.Properties)
foreach ($taskEntry in $taskPins) {
 if ((Get-FileHash -LiteralPath (Join-Path $taskApp $taskEntry.Name)).Hash -ne $taskEntry.Value) { throw 'M07 build changed; re-freeze the manual gate.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3'))) { throw "Missing isolated fixture: $taskData. Do not copy the normal profile." }
if ($CheckOnly) { [pscustomobject]@{ Result='PREFLIGHT PASS'; Scenario=$Scenario; DataRoot=$taskData; VerifiedBinaries=$taskPins.Count; Launched=$false }; return }
$taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
$taskElevated=([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$taskIdentity.Dispose()
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the previous DesktopUp normally; never launch during measurement.' }
$taskArguments='--show-control-center'
if ($ExitAfterMs -gt 0) { $taskArguments += " --exit-after-ms=$ExitAfterMs" }
$taskProbe=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskApp 'DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Normal
[pscustomobject]@{ Result='MANUAL PENDING'; Scenario=$Scenario; ProcessId=$taskProbe.Id; DataRoot=$taskData;
 LogPath=(Join-Path $taskData 'logs\desktopup.jsonl'); PowerShell=$PSVersionTable.PSVersion.ToString();
 Elevated=$taskElevated; EvidenceScope='current-environment'; AutoExitAfterMs=$ExitAfterMs }
