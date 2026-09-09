#requires -Version 5.1
param([switch]$CheckOnly, [switch]$Smoke, [ValidateRange(0,60000)][int]$ExitAfterMs=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskApp=Join-Path $taskRepo 'out\verify\v3-04-m07-menu-r1\app\DesktopUp.exe'
$taskFixture=Join-Path $taskRepo 'out\verify\v3-04-m07-menu-r1\fixture\DesktopUp.SessionFixture.dll'
if (-not (Test-Path -LiteralPath $taskApp) -or -not (Test-Path -LiteralPath $taskFixture)) { throw 'Build the menu R1 app and session fixture first.' }
$taskExpected=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'm07-menu-r1-build.json') -Raw | ConvertFrom-Json
foreach($taskEntry in $taskExpected.PSObject.Properties) {
 $taskFile=Join-Path (Split-Path -Parent $taskApp) $taskEntry.Name
 if (-not (Test-Path -LiteralPath $taskFile) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
  throw "R1 artifact fingerprint mismatch: $taskFile"
 }
}
if ($CheckOnly) {
 [pscustomobject]@{Result='PREFLIGHT ONLY';App=$taskApp;PowerShell=$PSVersionTable.PSVersion.ToString();Launched=$false}
 return
}
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the previous DesktopUp normally before starting the R1 copy.' }
$taskParent=Join-Path $taskRepo 'out\manual\v3-04-m07-menu-r1'
$null=New-Item -ItemType Directory -Path $taskParent -Force
$taskRun=(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
$taskData=Join-Path $taskParent $taskRun
& dotnet $taskFixture $taskData 3 --measure-expanded
if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed.' }
$taskArguments='--show-control-center'
if ($Smoke) { $taskArguments += ' --menu-smoke'; $ExitAfterMs=16000 }
if ($ExitAfterMs -gt 0) { $taskArguments += " --exit-after-ms=$ExitAfterMs" }
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskApp -DataRoot $taskData -Arguments $taskArguments -WindowStyle Normal -MenuDiagnostics
if ($Smoke) {
 $taskProcess.WaitForExit()
 $taskLog=Join-Path $taskData 'logs\desktopup.jsonl'
 $taskRows=@(Get-Content -LiteralPath $taskLog | ForEach-Object { $_ | ConvertFrom-Json })
 $taskFinal=@($taskRows | Where-Object { $_.eventName -eq 'menu.smoke_completed' })
 $taskErrors=@($taskRows | Where-Object { $_.level -in @('error','critical') })
 $taskLedger=Get-Content -LiteralPath (Join-Path $taskData 'state\resource-ledger.json') -Raw | ConvertFrom-Json
 if ($taskProcess.ExitCode -ne 0 -or $taskFinal.Count -ne 1 -or -not $taskFinal[0].data.passed -or $taskErrors.Count -ne 0 -or
     -not $taskLedger.clean -or @($taskLedger.resources).Count -ne 0) {
   throw "R1 smoke failed; inspect $taskLog"
 }
}
[pscustomobject]@{Result=$(if($Smoke){'NATIVE PROBE PASS - MOUSE VALIDATION PENDING'}else{'STARTED - MANUAL VALIDATION PENDING'});
 ProcessId=$taskProcess.Id;DataRoot=$taskData;LogPath=(Join-Path $taskData 'logs\desktopup.jsonl');
 AppDllSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $taskApp) 'DesktopUp.dll')).Hash;
 PowerShell=$PSVersionTable.PSVersion.ToString();AutoExitAfterMs=$ExitAfterMs}
