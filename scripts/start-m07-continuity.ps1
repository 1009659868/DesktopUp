#requires -Version 5.1
param([switch]$CheckOnly, [switch]$Smoke)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskApp=Join-Path $taskRepo 'out\verify\v3-04-m07-continuity\app\DesktopUp.exe'
$taskFixture=Join-Path $taskRepo 'out\verify\v3-04-m07-continuity\fixture\DesktopUp.SessionFixture.dll'
if (-not (Test-Path -LiteralPath $taskApp) -or -not (Test-Path -LiteralPath $taskFixture)) { throw 'Build continuity app and session fixture first.' }
$taskExpected=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'm07-continuity-build.json') -Raw | ConvertFrom-Json
foreach($taskArea in $taskExpected.PSObject.Properties) {
 $taskDirectory=Join-Path (Join-Path $taskRepo 'out\verify\v3-04-m07-continuity') $taskArea.Name
 foreach($taskEntry in $taskArea.Value.PSObject.Properties) {
  $taskFile=Join-Path $taskDirectory $taskEntry.Name
  if (-not (Test-Path -LiteralPath $taskFile) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
   throw "Continuity artifact fingerprint mismatch: $taskFile"
  }
 }
}
if ($CheckOnly) { [pscustomobject]@{Result='PREFLIGHT ONLY';App=$taskApp;PowerShell=$PSVersionTable.PSVersion.ToString()}; return }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the previous DesktopUp normally before starting this isolated copy.' }
$taskParent=Join-Path $taskRepo 'out\manual\v3-04-m07-continuity'
$null=New-Item -ItemType Directory -Path $taskParent -Force
$taskData=Join-Path $taskParent ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
& dotnet $taskFixture $taskData 3 --measure-collapsed
if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed.' }
$taskArgs=$(if($Smoke){'--continuity-smoke --exit-after-ms=30000'}else{'--continuity-arrange'})
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskApp -DataRoot $taskData -Arguments $taskArgs -WindowStyle Normal
if ($Smoke) {
 $taskProcess.WaitForExit()
 $taskRows=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
 $taskFinal=@($taskRows | Where-Object { $_.eventName -eq 'continuity.smoke_completed' })
 $taskErrors=@($taskRows | Where-Object { $_.level -in @('error','critical') })
 $taskLedger=Get-Content -LiteralPath (Join-Path $taskData 'state\resource-ledger.json') -Raw | ConvertFrom-Json
 if ($taskProcess.ExitCode -ne 0 -or $taskFinal.Count -ne 1 -or -not $taskFinal[0].data.passed -or $taskErrors.Count -ne 0 -or
     -not $taskLedger.clean -or @($taskLedger.resources).Count -ne 0) { throw "Continuity probe failed; inspect $taskData" }
}
[pscustomobject]@{Result=$(if($Smoke){'NATIVE CONTINUITY PASS - VISUAL VALIDATION PENDING'}else{'STARTED - C/B/A COMPACT CHAIN'});
 ProcessId=$taskProcess.Id;DataRoot=$taskData;LogPath=(Join-Path $taskData 'logs\desktopup.jsonl');
 AppDllSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $taskApp) 'DesktopUp.dll')).Hash}
