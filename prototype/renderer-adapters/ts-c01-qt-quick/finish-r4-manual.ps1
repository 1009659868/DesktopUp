[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^r4-manual-[A-Za-z0-9_-]+$')]
    [string]$SessionId,

    [ValidateSet('NOT_REPORTED', 'PASS', 'FAIL')]
    [string]$UserObservation = 'NOT_REPORTED',

    [string]$Note = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$candidateRoot = $PSScriptRoot
$sessionRoot = Join-Path (Join-Path $candidateRoot 'out') $SessionId
$manifestPath = Join-Path $sessionRoot 'session.json'
$eventLog = Join-Path $sessionRoot 'events.jsonl'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "R4 session manifest not found: $manifestPath"
}
if (-not (Test-Path -LiteralPath $eventLog -PathType Leaf)) {
    throw "R4 event log not found: $eventLog"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$process = Get-Process -Id ([int]$manifest.processId) -ErrorAction SilentlyContinue
if ($null -ne $process) {
    throw "R4 session process is still running: PID=$($manifest.processId)"
}

$e5Root = (Resolve-Path -LiteralPath (Join-Path $candidateRoot '..\..\desktop-host-win32\ts-h01-e5')).Path
$validator = Join-Path $e5Root 'validate-desktop-host-events.ps1'
& $validator -Path $eventLog
if ($LASTEXITCODE -ne 0) {
    throw "E5 validation failed with exit code $LASTEXITCODE"
}

$events = @(Get-Content -LiteralPath $eventLog | ForEach-Object { $_ | ConvertFrom-Json })
$cleanup = @($events | Where-Object event -eq 'cleanup_completed' | Select-Object -Last 1)
$ended = @($events | Where-Object event -eq 'host_session_ended' | Select-Object -Last 1)
$cleanupZero = $cleanup.Count -eq 1 -and $ended.Count -eq 1 -and
    [int]$cleanup[0].resource_counts.adapter -eq 0 -and
    [int]$cleanup[0].resource_counts.host -eq 0 -and
    [int]$cleanup[0].resource_counts.quarantine -eq 0
if (-not $cleanupZero) {
    throw 'R4 session did not end with zero adapter/host/quarantine resources.'
}

$manifest | Add-Member -NotePropertyName completedAt -NotePropertyValue ((Get-Date).ToString('o')) -Force
$manifest | Add-Member -NotePropertyName processExited -NotePropertyValue $true -Force
$manifest | Add-Member -NotePropertyName eventCount -NotePropertyValue $events.Count -Force
$manifest | Add-Member -NotePropertyName e5Validation -NotePropertyValue 'PASS' -Force
$manifest | Add-Member -NotePropertyName cleanupZero -NotePropertyValue $true -Force
$manifest | Add-Member -NotePropertyName userObservation -NotePropertyValue $UserObservation -Force
$manifest | Add-Member -NotePropertyName observationNote -NotePropertyValue $Note -Force
$status = if ($UserObservation -eq 'PASS') {
    'SESSION_PASS_NOT_OVERALL_P0'
} elseif ($UserObservation -eq 'FAIL') {
    'SESSION_FAIL_RETAIN_FOR_DIAGNOSIS'
} else {
    'AUTOMATED_LOG_PASS_AWAITING_USER_OBSERVATION'
}
$manifest | Add-Member -NotePropertyName status -NotePropertyValue $status -Force
$manifest | Add-Member -NotePropertyName p0Result -NotePropertyValue 'NOT_RUN' -Force
$manifest | Add-Member -NotePropertyName candidateEvidenceCreated -NotePropertyValue $false -Force
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Output "R4_SESSION_FINISHED=$SessionId"
Write-Output "STATUS=$status"
Write-Output "EVENT_COUNT=$($events.Count)"
Write-Output 'E5_VALIDATION=PASS'
Write-Output 'CLEANUP_ZERO=true'
Write-Output 'P0_RESULT=NOT_RUN'