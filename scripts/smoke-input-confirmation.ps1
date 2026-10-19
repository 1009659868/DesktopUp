#requires -Version 5.1
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot, [switch]$ExitWithDialog)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$exe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$isolatedRoot = [IO.Path]::GetFullPath($DataRoot)
$normalRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3'))
if ($isolatedRoot.TrimEnd('\') -eq $normalRoot -or $isolatedRoot.StartsWith($normalRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated multi-poster fixture, never the normal user profile.'
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Close the existing DesktopUp normally before testing.' }
$probeArguments = @('--input-confirmation-smoke','--show-control-center','--exit-after-ms=20000')
if ($ExitWithDialog) { $probeArguments += '--exit-with-confirmation' }
$probe = Start-DesktopUpFixtureProcess -ExecutablePath $exe -DataRoot $isolatedRoot -Arguments ($probeArguments -join ' ') -WindowStyle Hidden
if (-not $probe.WaitForExit(25000)) { throw "Input confirmation smoke did not exit: PID $($probe.Id)." }
$probe.Refresh()
$events = @(Get-Content -LiteralPath (Join-Path $isolatedRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object { $_.processId -eq $probe.Id })
$completed = @($events | Where-Object { $_.eventName -eq 'input_confirmation.smoke_completed' -and $_.data.passed })
$opened = @($events | Where-Object { $_.eventName -eq 'poster.removal_confirmation_opened' })
$errors = @($events | Where-Object { $_.level -in 'error','critical' })
$ledger = Get-Content -Raw -LiteralPath (Join-Path $isolatedRoot 'state\resource-ledger.json') | ConvertFrom-Json
$expectedDialogs = if ($ExitWithDialog) { 3 } else { 2 }
if ($probe.ExitCode -ne 0 -or $completed.Count -ne 1 -or $opened.Count -ne $expectedDialogs -or $errors.Count -gt 0 -or
    -not $ledger.clean -or @($ledger.resources).Count -gt 0) {
    throw "Input smoke failed: PID=$($probe.Id), exit=$($probe.ExitCode), dialogs=$($opened.Count), errors=$($errors.Count). Inspect $isolatedRoot\logs\desktopup.jsonl"
}
[pscustomobject]@{ Result='PASS'; ProcessId=$probe.Id; ExitCode=$probe.ExitCode; DialogsOpened=$opened.Count;
    ReadyInputClusters=$completed[0].data.readyInputClusters; StateRevision=$completed[0].data.revision;
    ActiveResourcesAfterExit=@($ledger.resources).Count; DataRoot=$isolatedRoot }
