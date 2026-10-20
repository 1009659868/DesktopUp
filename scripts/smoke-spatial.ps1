#requires -Version 5.1
param([Parameter(Mandatory)][string]$ExecutablePath, [Parameter(Mandatory)][string]$DataRoot,
    [ValidateSet('Full','Constrained','InsufficientSpace')][string]$ExpectedExpansion, [switch]$LeaveExpanded, [string]$AnimationConfig)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$exe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$isolatedRoot = [IO.Path]::GetFullPath($DataRoot)
$normalRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3'))
if ($isolatedRoot.TrimEnd('\') -eq $normalRoot -or $isolatedRoot.StartsWith($normalRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated fixture, never the normal user profile.'
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Close the existing DesktopUp normally before testing this binary.' }
$taskArguments = @('--spatial-smoke','--show-control-center','--exit-after-ms=20000')
if ($ExpectedExpansion) { $taskArguments += "--expect-expansion=$ExpectedExpansion" }
if ($LeaveExpanded) { $taskArguments += '--spatial-smoke-leave-expanded' }
if ($AnimationConfig) { $taskArguments += '--animation-config="' + (Get-Item -LiteralPath $AnimationConfig).FullName + '"' }
$probe = Start-DesktopUpFixtureProcess -ExecutablePath $exe -DataRoot $isolatedRoot -Arguments ($taskArguments -join ' ')
if (-not $probe.WaitForExit(25000)) { throw "Spatial smoke did not exit: PID $($probe.Id)." }
$probe.Refresh()
$events = @(Get-Content -LiteralPath (Join-Path $isolatedRoot 'logs\desktopup.jsonl') | ConvertFrom-Json | Where-Object { $_.processId -eq $probe.Id })
$completed = @($events | Where-Object { $_.eventName -eq 'spatial.smoke_completed' -and $_.data.passed })
$errors = @($events | Where-Object { $_.level -in 'error','critical' })
$ledger = Get-Content -Raw -LiteralPath (Join-Path $isolatedRoot 'state\resource-ledger.json') | ConvertFrom-Json
$postures = @($events | Where-Object { $_.eventName -eq 'spatial.posture_started' })
$taskFrames = @($events | Where-Object eventName -eq 'spatial.probe_frame')
$taskOutcomes = @($events | Where-Object { $_.eventName -eq 'spatial.smoke_outcome' })
$taskPostureValid = if ($ExpectedExpansion -eq 'InsufficientSpace') { $postures.Count -eq 0 -and $taskOutcomes.Count -gt 0 -and
    @($events | Where-Object { $_.eventName -eq 'spatial.expansion_feedback' }).Count -eq 1 } else { $postures.Count -ge $(if ($LeaveExpanded) { 1 } else { 2 }) }
if ($probe.ExitCode -ne 0 -or $completed.Count -ne 1 -or -not $taskPostureValid -or $errors.Count -gt 0 -or
    -not $ledger.clean -or @($ledger.resources).Count -gt 0) {
    throw "Spatial smoke failed: PID=$($probe.Id), exit=$($probe.ExitCode), completed=$($completed.Count), errors=$($errors.Count). Inspect $isolatedRoot\logs\desktopup.jsonl"
}
[pscustomobject]@{ Result='PASS'; ProcessId=$probe.Id; ExitCode=$probe.ExitCode; PostureCommands=$postures.Count;
    NativeFrames=$taskFrames.Count;
    RefinedProofFrames=($postures | ForEach-Object { $_.data.frames } | Measure-Object -Sum).Sum; ActiveResourcesAfterExit=@($ledger.resources).Count;
    Outcomes=$taskOutcomes.data; DataRoot=$isolatedRoot }
