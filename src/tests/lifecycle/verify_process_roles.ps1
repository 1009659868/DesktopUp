param(
  [Parameter(Mandatory = $true)]
  [string]$RuntimePath,
  [Parameter(Mandatory = $true)]
  [string]$ControlCenterPath,
  [Parameter(Mandatory = $true)]
  [string]$FixtureRoot
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $FixtureRoot -Force | Out-Null
$pipeName = "DesktopUp.QTS03.ProcessRoles.$PID"
$instanceName = "DesktopUp.QTS03.ProcessRoles.$PID"
$statePath = Join-Path $FixtureRoot 'state.sqlite'
$ledgerPath = Join-Path $FixtureRoot 'ledger.json'
$runtimeArguments = @(
  '--state-db', $statePath,
  '--pipe-name', $pipeName,
  '--instance-name', $instanceName,
  '--ledger', $ledgerPath,
  '--exit-after-ms', '3000'
)

$primary = Start-Process -FilePath $RuntimePath -ArgumentList $runtimeArguments -WindowStyle Hidden -PassThru
try {
  Start-Sleep -Milliseconds 100

  $secondary = Start-Process -FilePath $RuntimePath -ArgumentList $runtimeArguments -WindowStyle Hidden -PassThru -Wait
  if ($secondary.ExitCode -ne 0) {
    throw "QTS03_SECONDARY_FORWARD_FAILED: $($secondary.ExitCode)"
  }
  if ($primary.HasExited) {
    throw 'QTS03_PRIMARY_EXITED_AFTER_SECONDARY'
  }

  $control = Start-Process -FilePath $ControlCenterPath -ArgumentList @('--pipe-name', $pipeName, '--smoke-ms', '100') -WindowStyle Hidden -PassThru -Wait
  if ($control.ExitCode -ne 0) {
    throw "QTS03_CONTROL_CENTER_HANDSHAKE_FAILED: $($control.ExitCode)"
  }
  if ($primary.HasExited) {
    throw 'QTS03_RUNTIME_EXITED_WHEN_CONTROL_CENTER_CLOSED'
  }

  if (-not $primary.WaitForExit(5000)) {
    throw 'QTS03_PRIMARY_EXIT_TIMEOUT'
  }
  if ($primary.ExitCode -ne 0) {
    throw "QTS03_PRIMARY_EXIT_FAILED: $($primary.ExitCode)"
  }

  $ledger = Get-Content -LiteralPath $ledgerPath -Raw | ConvertFrom-Json
  if ($ledger.terminalState -ne 'exited' -or [int64]$ledger.ownedResourceCount -ne 0) {
    throw 'QTS03_PROCESS_ROLE_LEDGER_FAILED'
  }
} finally {
  if (-not $primary.HasExited) {
    Stop-Process -Id $primary.Id -Force -ErrorAction SilentlyContinue
  }
}

Write-Output 'QTS03_PROCESS_ROLES_PASS'
