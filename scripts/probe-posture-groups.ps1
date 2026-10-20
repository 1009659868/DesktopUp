#requires -Version 7
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedAssemblySha256,
    [switch]$Combinations
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$rrApp = (Resolve-Path -LiteralPath $AppDirectory).Path
$rrFixture = (Resolve-Path -LiteralPath $FixtureDirectory).Path
$rrRoot = [IO.Path]::GetFullPath($OutputRoot)
$rrAllowed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../out')) + '\'
if (!$rrRoot.StartsWith($rrAllowed,[StringComparison]::OrdinalIgnoreCase) -or
    $rrRoot.StartsWith($rrApp,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $rrRoot)) { throw 'Use a NEW project out directory outside the candidate.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first; no process stopped.' }
if ((Get-FileHash -LiteralPath (Join-Path $rrApp 'DesktopUp.dll')).Hash -ine $ExpectedAssemblySha256) { throw 'Candidate hash mismatch.' }
New-Item -ItemType Directory -Path $rrRoot | Out-Null
$rrBinding = @(Get-ChildItem -LiteralPath $rrApp -Recurse -File | ForEach-Object { [pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$rrBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rrRoot 'artifacts.json')
$rrResults = @()
foreach ($rrCase in @('independent','chain')) {
    $rrData = Join-Path $rrRoot $rrCase
    $rrFixtureArgs = @($rrData, '2')
    if ($rrCase -eq 'chain') { $rrFixtureArgs += '--compact-pair' }
    & (Join-Path $rrFixture 'DesktopUp.SessionFixture.exe') @rrFixtureArgs
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
    $rrArgs = '--posture-groups-probe --exit-after-ms=60000'
    if ($Combinations) { $rrArgs += ' --rr-combinations' }
    if ($rrCase -eq 'chain') { $rrArgs += ' --rr-chain' }
    $rrProcess = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $rrApp 'DesktopUp.exe') -DataRoot $rrData -Arguments $rrArgs -WindowStyle Hidden
    if (!$rrProcess.WaitForExit(65000)) { throw "Timed out; process preserved: $($rrProcess.Id)" }
    $rrEvents = @(Get-Content -LiteralPath (Join-Path $rrData 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $rrProcess.Id)
    $rrDone = @($rrEvents | Where-Object eventName -eq 'rr.groups_observed')
    $rrExit = @($rrEvents | Where-Object eventName -eq 'application.stopped')
    $rrErrors = @($rrEvents | Where-Object level -in @('error','critical','fatal'))
    $rrPass = $rrProcess.ExitCode -eq 0 -and $rrDone.Count -eq 1 -and $rrDone[0].data.passed -and $rrExit.Count -eq 1 -and $rrExit[0].data.activeResources -eq 0 -and $rrErrors.Count -eq 0
    $rrResult = [pscustomobject]@{Case=$rrCase;Passed=$rrPass;ProcessId=$rrProcess.Id;ExitCode=$rrProcess.ExitCode;Observation=$rrDone.data;Errors=$rrErrors;Cleanup=$rrExit.data}
    $rrResults += $rrResult
    $rrResult | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $rrRoot ($rrCase+'.json'))
    if (!$rrPass) { throw "RR group failed: $rrCase; evidence preserved in $rrRoot" }
}
foreach ($rrFile in $rrBinding) { if ((Get-FileHash -LiteralPath $rrFile.Path).Hash -cne $rrFile.SHA256) { throw 'Candidate changed during test.' } }
$rrResults | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $rrRoot 'results.json')
[pscustomobject]@{Result='PASS - native surfaces/managed commands, NOT native input';Processes=$rrResults.ProcessId;OutputRoot=$rrRoot}
