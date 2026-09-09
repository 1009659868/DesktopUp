param(
    [ValidateSet('interaction', 'chain', 'removal', 'return-sync', 'expand-down-first', 'expand-up', 'expand-mixed', 'expand-constrained', 'expand-blocked')]
    [string]$Scenario = 'expand-down-first',
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskAppDirectory = Join-Path $taskRepository 'out\verify\v3-04-m04-07-r2-down-first\app'
# Preserve the historical fixture name as an alias, not its superseded pure-up expectation.
$taskFixtureName = if ($Scenario -eq 'expand-down-first') { 'expand-up' } else { $Scenario }
$taskDataRoot = Join-Path $taskRepository "out\manual\v3-04-m04-07-r2-down-first\$taskFixtureName"
$taskExpectedHashes = @{
    'DesktopUp.exe' = '958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0'
    'DesktopUp.dll' = '75C2C4BB82DC11136850E4D2CB3E7CF9F1800398AF7B2D756CD45467D591846C'
    'DesktopUp.Windows.dll' = 'F8AC596994670DA230BC4EA1FF523344877FAE3CAD061DED3088E1C562E0E65F'
    'DesktopUp.Application.dll' = '34B8C88DB2EBB995C3C6C391D6F05BDF76889637F576F3516ED62DE60D8594E8'
    'DesktopUp.Domain.dll' = '92D2F76FB78858BEAF94C6168CDCA603FD71300A3E5C98302F39AA7A48B39F36'
    'DesktopUp.Infrastructure.dll' = '9C7995E1029EA5341843C3F44A5737211A6BC4CB5DC541D7233C17521B67B5D6'
}
foreach ($taskEntry in $taskExpectedHashes.GetEnumerator()) {
    $taskFile = Join-Path $taskAppDirectory $taskEntry.Key
    if (-not (Test-Path -LiteralPath $taskFile -PathType Leaf) -or
        (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskEntry.Value) {
        throw "Manual-test build is missing or changed: $taskFile. Re-freeze the test build before continuing."
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $taskDataRoot 'state\desktopup.sqlite3') -PathType Leaf)) {
    throw "Isolated fixture is missing: $taskDataRoot. This script does not create or overwrite profiles."
}
if ($CheckOnly) {
    [pscustomobject]@{ Result = 'PREFLIGHT PASS'; Scenario = $Scenario; DataRoot = $taskDataRoot; VerifiedBinaries = $taskExpectedHashes.Count; Launched = $false }
    return
}
$taskExisting = @(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue)
if ($taskExisting.Count -gt 0) {
    throw "Close the existing DesktopUp normally first. Running PID(s): $($taskExisting.Id -join ', '). No process was stopped."
}
$taskPreviousRoot = [Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT', 'Process')
try {
    [Environment]::SetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT', $taskDataRoot, 'Process')
    # This is the user's visible manual test, not a background smoke or automatic confirmation.
    $taskProcess = Start-Process -FilePath (Join-Path $taskAppDirectory 'DesktopUp.exe') -ArgumentList '--show-control-center' -WindowStyle Normal -PassThru
    [pscustomobject]@{ Result = 'STARTED - MANUAL RESULTS PENDING'; Scenario = $Scenario; ProcessId = $taskProcess.Id;
        DataRoot = $taskDataRoot; LogPath = (Join-Path $taskDataRoot 'logs\desktopup.jsonl') }
}
finally {
    [Environment]::SetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT', $taskPreviousRoot, 'Process')
}
