#requires -Version 7.4
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot)
$ErrorActionPreference = 'Stop'
$taskExe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$taskRoot = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3'))
if ($taskRoot -eq $taskNormal -or $taskRoot.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated three-poster SessionFixture, never the normal user profile.'
}
if (-not (Test-Path -LiteralPath $taskRoot -PathType Container)) { throw 'Create the isolated fixture first.' }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Close the existing DesktopUp normally before testing this binary.' }
$taskProbe = Start-Process -FilePath $taskExe -ArgumentList @('--presentation-smoke','--show-control-center','--exit-after-ms=17000') `
    -WindowStyle Hidden -PassThru -Environment @{ DESKTOPUP_V3_TEST_DATA_ROOT = $taskRoot }
if (-not $taskProbe.WaitForExit(22000)) { throw "Presentation smoke did not exit: PID $($taskProbe.Id)." }
$taskProbe.Refresh()
$taskEvents = @(Get-Content -LiteralPath (Join-Path $taskRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $taskProbe.Id)
$taskDone = @($taskEvents | Where-Object { $_.eventName -eq 'presentation.smoke_completed' -and $_.data.passed })
$taskErrors = @($taskEvents | Where-Object level -in 'error','critical')
$taskLedger = Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'state\resource-ledger.json') | ConvertFrom-Json
if ($taskProbe.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or $taskErrors.Count -gt 0 -or -not $taskLedger.clean -or @($taskLedger.resources).Count -gt 0) {
    throw "Presentation smoke failed: PID=$($taskProbe.Id), exit=$($taskProbe.ExitCode), completed=$($taskDone.Count), errors=$($taskErrors.Count). Inspect $taskRoot\logs\desktopup.jsonl"
}
[pscustomobject]@{ Result='PASS'; ProcessId=$taskProbe.Id; ExitCode=$taskProbe.ExitCode; PosterCount=$taskDone[0].data.posterCount;
    PersistedVersionedAtStart=($taskEvents | Where-Object eventName -eq 'presentation.smoke_start').data.persistedVersioned;
    Readback=($taskEvents | Where-Object eventName -eq 'presentation.smoke_readback').data; ActiveResourcesAfterExit=@($taskLedger.resources).Count; DataRoot=$taskRoot }
