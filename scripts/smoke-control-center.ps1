#requires -Version 7.4
param(
    [Parameter(Mandatory)]
    [string]$ExecutablePath,
    [Parameter(Mandatory)]
    [string]$DataRoot,
    [ValidateRange(0, 20)]
    [int]$ExpectedPosterCount = 1
)

$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $ExecutablePath).Path
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) {
    throw 'Close the running DesktopUp normally before this smoke; it must test the selected binary.'
}
$isolatedRoot = [IO.Path]::GetFullPath($DataRoot)
$normalRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3'))
if ($isolatedRoot.TrimEnd('\') -eq $normalRoot.TrimEnd('\') -or
    $isolatedRoot.StartsWith($normalRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Smoke DataRoot must not point inside the real user profile.'
}
$logPath = Join-Path $isolatedRoot 'logs\desktopup.jsonl'
$ledgerPath = Join-Path $isolatedRoot 'state\resource-ledger.json'
$testEnvironment = @{ DESKTOPUP_V3_TEST_DATA_ROOT = $isolatedRoot }
$probe = Start-Process -FilePath $exe -ArgumentList '--show-control-center','--exit-after-ms=10000' -Environment $testEnvironment -PassThru -WindowStyle Hidden
$deadline = [DateTime]::UtcNow.AddSeconds(7)
$shown = $false
while (-not $probe.HasExited -and [DateTime]::UtcNow -lt $deadline) {
    if (Test-Path -LiteralPath $logPath) {
        $shown = @(Get-Content -LiteralPath $logPath -Tail 150 |
            ConvertFrom-Json |
            Where-Object { $_.processId -eq $probe.Id -and $_.eventName -eq 'control_center.shown' }).Count -gt 0
        if ($shown) { break }
    }
    Start-Sleep -Milliseconds 200
}
if ($shown) {
    # Exercise the same ShowControlCenter dispatcher used by the tray, via single-instance activation.
    $secondary = Start-Process -FilePath $exe -ArgumentList '--show-control-center' -Environment $testEnvironment -PassThru -WindowStyle Hidden
    if (-not $secondary.WaitForExit(10000)) { throw 'Secondary activation did not exit.' }
    if ($secondary.ExitCode -ne 0) { throw "Secondary activation failed: $($secondary.ExitCode)" }
}
if (-not $probe.WaitForExit(20000)) { throw 'Control center smoke did not exit within its timeout.' }
$probe.Refresh()
$events = @(Get-Content -LiteralPath $logPath | ConvertFrom-Json |
    Where-Object { $_.processId -eq $probe.Id })
$errors = @($events | Where-Object { $_.level -in 'error','critical' })
$created = @($events | Where-Object { $_.eventName -eq 'control_center.created' }).Count
$showCount = @($events | Where-Object { $_.eventName -eq 'control_center.shown' }).Count
$projected = @($events | Where-Object { $_.eventName -eq 'control_center.projection_applied' }).Count
$posterShown = @($events | Where-Object { $_.eventName -eq 'poster.shown' }).Count
$posterIds = @($events | Where-Object { $_.eventName -eq 'poster.shown' } | ForEach-Object { $_.data.posterId } | Sort-Object -Unique)
$sharedMonitors = @($events | Where-Object { $_.eventName -eq 'desktop_environment.monitoring_started' }).Count
$committedRecovery = @($events | Where-Object { $_.eventName -eq 'desktop_environment.recovery_committed' })
$stopped = @($events | Where-Object { $_.eventName -eq 'application.stopped' -and $_.data.activeResources -eq 0 }).Count
$ledger = Get-Content -Raw -LiteralPath $ledgerPath | ConvertFrom-Json
if ($probe.ExitCode -ne 0 -or $errors.Count -ne 0 -or $created -ne 1 -or
    $showCount -lt 2 -or $projected -lt 1 -or $posterShown -ne $ExpectedPosterCount -or $stopped -ne 1 -or
    $posterIds.Count -ne $ExpectedPosterCount -or $sharedMonitors -ne 1 -or $committedRecovery.Count -lt 1 -or
    -not $ledger.clean -or @($ledger.resources).Count -ne 0 -or
    (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue)) {
    throw "Control center smoke failed: exit=$($probe.ExitCode), errors=$($errors.Count), created=$created, shown=$showCount, projected=$projected, poster=$posterShown, stopped=$stopped. Inspect $logPath."
}
[pscustomobject]@{
    Result = 'PASS'
    DataRoot = $isolatedRoot
    ProcessId = $probe.Id
    ExitCode = $probe.ExitCode
    ControlCentersCreated = $created
    ShowRequestsCompleted = $showCount
    ProjectionsApplied = $projected
    PostersCreated = $posterShown
    PosterIds = $posterIds
    SharedEnvironmentMonitors = $sharedMonitors
    ActiveResourcesAfterExit = @($ledger.resources).Count
}
