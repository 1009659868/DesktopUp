param(
  [Parameter(Mandatory = $true)]
  [string]$LedgerPath
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $LedgerPath -PathType Leaf)) {
  throw "QTS03_EXIT_LEDGER_MISSING: $LedgerPath"
}

$ledger = Get-Content -LiteralPath $LedgerPath -Raw | ConvertFrom-Json
if ($ledger.terminalState -ne 'exited') {
  throw "QTS03_EXIT_TERMINAL_STATE_INVALID: $($ledger.terminalState)"
}
if ([int64]$ledger.ownedResourceCount -ne 0) {
  throw "QTS03_EXIT_OWNED_RESOURCES_REMAIN: $($ledger.ownedResourceCount)"
}

$fixtureRoot = Split-Path -Parent $LedgerPath
$transient = Get-ChildItem -LiteralPath $fixtureRoot -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -like '*.sqlite-wal' -or $_.Name -like '*.sqlite-shm' -or $_.Name -like '*.tmp' }
if ($transient) {
  throw "QTS03_EXIT_TRANSIENT_FILES_REMAIN: $($transient.Name -join ', ')"
}

Write-Output 'QTS03_EXIT_LEDGER_PASS'
