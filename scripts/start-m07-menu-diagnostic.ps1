#requires -Version 5.1
param([switch]$CheckOnly, [ValidateRange(0,60000)][int]$ExitAfterMs=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskApp=Join-Path $taskRepo 'out\verify\v3-04-m07-menu-diagnostic\app\DesktopUp.exe'
$taskFixture=Join-Path $taskRepo 'tests\DesktopUp.SessionFixture\bin\x64\Release\net10.0-windows10.0.22621.0\DesktopUp.SessionFixture.dll'
if (-not (Test-Path -LiteralPath $taskApp) -or -not (Test-Path -LiteralPath $taskFixture)) {
 throw 'Build the separate menu diagnostic app and DesktopUp.SessionFixture first.'
}
if ($CheckOnly) {
 [pscustomobject]@{Result='PREFLIGHT ONLY'; App=$taskApp; Fixture=$taskFixture; PowerShell=$PSVersionTable.PSVersion.ToString(); Launched=$false}
 return
}
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the previous DesktopUp normally before starting the diagnostic copy.' }
$taskParent=Join-Path $taskRepo 'out\manual\v3-04-m07-menu-diagnostic'
$null=New-Item -ItemType Directory -Path $taskParent -Force
$taskRun=(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
$taskData=Join-Path $taskParent $taskRun
& dotnet $taskFixture $taskData 3 --measure-expanded
if ($LASTEXITCODE -ne 0) { throw 'Fresh isolated menu fixture creation failed.' }
$taskArguments='--show-control-center'
if ($ExitAfterMs -gt 0) { $taskArguments += " --exit-after-ms=$ExitAfterMs" }
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskApp -DataRoot $taskData -Arguments $taskArguments -WindowStyle Normal -MenuDiagnostics
[pscustomobject]@{Result='DIAGNOSTIC ONLY - ROOT CAUSE PENDING'; ProcessId=$taskProcess.Id;
 DataRoot=$taskData; LogPath=(Join-Path $taskData 'logs\desktopup.jsonl');
 AppDllSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $taskApp) 'DesktopUp.dll')).Hash;
 PowerShell=$PSVersionTable.PSVersion.ToString(); AutoExitAfterMs=$ExitAfterMs}
