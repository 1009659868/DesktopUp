$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\DesktopUp.App\bin\x64\Release\net10.0-windows10.0.22621.0\win-x64\DesktopUp.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}

$process = Start-Process -FilePath $exe -ArgumentList '--exit-after-ms=3000' -PassThru
if (-not $process.WaitForExit(15000)) {
    throw 'DesktopUp did not exit within the desktop locator smoke timeout.'
}
if ($process.ExitCode -ne 0) {
    throw "DesktopUp desktop locator smoke exited with code $($process.ExitCode)."
}

$logPath = Join-Path $env:LOCALAPPDATA 'DesktopUp\V3\logs\desktopup.jsonl'
$events = Get-Content -LiteralPath $logPath |
    ForEach-Object { $_ | ConvertFrom-Json } |
    Where-Object { $_.processId -eq $process.Id }
$stable = @($events | Where-Object { $_.eventName -eq 'desktop_shell.stable' })
if ($stable.Count -ne 1) {
    throw "Expected one stable desktop shell event for PID $($process.Id), found $($stable.Count)."
}
if ($stable[0].data.generation -ne 1 -or
    [string]::IsNullOrWhiteSpace($stable[0].data.iconViewHandle) -or
    [string]::IsNullOrWhiteSpace($stable[0].data.containerHandle)) {
    throw 'Stable desktop shell event did not contain the expected generation and handles.'
}
$attached = @($events | Where-Object { $_.eventName -eq 'poster.attached' -and $_.data.Succeeded })
$inputIslandCreated = @($events | Where-Object { $_.eventName -eq 'poster.input_island_created' })
$inputIslandDisposed = @($events | Where-Object { $_.eventName -eq 'poster.input_island_disposed' })
$shown = @($events | Where-Object { $_.eventName -eq 'poster.shown' })
$topology = @($events | Where-Object { $_.eventName -eq 'display_topology.stable' })
$monitoringStarted = @($events | Where-Object { $_.eventName -eq 'desktop_environment.monitoring_started' })
$monitoringStopped = @($events | Where-Object { $_.eventName -eq 'desktop_environment.monitoring_stopped' })
$errors = @($events | Where-Object { $_.level -eq 'error' })
if ($attached.Count -ne 1 -or $shown.Count -ne 1) {
    throw "Expected one attached and shown poster event; attached=$($attached.Count), shown=$($shown.Count)."
}
if ($inputIslandCreated.Count -ne 1 -or $inputIslandDisposed.Count -ne 1) {
    throw "Expected one created and disposed poster input island; created=$($inputIslandCreated.Count), disposed=$($inputIslandDisposed.Count)."
}
if ($shown[0].data.inputHandle -ne $inputIslandCreated[0].data.handle -or
    $shown[0].data.desktopGeneration -ne $inputIslandCreated[0].data.Generation) {
    throw 'Poster visual and input island were not committed in the same desktop generation.'
}
if ($topology.Count -ne 1 -or
    $topology[0].data.generation -ne 1 -or
    $monitoringStarted.Count -ne 1 -or
    $monitoringStopped.Count -ne 1) {
    throw "Display/monitoring lifecycle mismatch; topology=$($topology.Count), started=$($monitoringStarted.Count), stopped=$($monitoringStopped.Count)."
}
if ($errors.Count -ne 0) {
    throw "Desktop locator smoke recorded $($errors.Count) error event(s)."
}

$ledgerPath = Join-Path $env:LOCALAPPDATA 'DesktopUp\V3\state\resource-ledger.json'
$ledger = Get-Content -Raw -LiteralPath $ledgerPath | ConvertFrom-Json
if (-not $ledger.clean -or @($ledger.resources).Count -ne 0) {
    throw 'DesktopUp resource ledger was not clean after desktop locator smoke.'
}

Write-Host "V3-03 poster cluster smoke: PASS (PID=$($process.Id), generation=1, visual+input=clean)"
