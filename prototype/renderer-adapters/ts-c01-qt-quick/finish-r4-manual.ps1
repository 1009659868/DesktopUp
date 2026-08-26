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
$manualFinished = @($events | Where-Object event -eq 'r4_manual_session_finished' | Select-Object -Last 1)
if ($manualFinished.Count -eq 1 -and [string]$manualFinished[0].manual_phase -eq 'R4C') {
    $r4cMetrics = [ordered]@{
        shellRecoveryCount = [int]$manualFinished[0].shell_recovery_count
        shellDegradedCount = [int]$manualFinished[0].shell_degraded_count
        shellDegradedAtFinish = [bool]$manualFinished[0].shell_degraded_at_finish
        controlCenterProbeOpenCount = [int]$manualFinished[0].control_center_probe_open_count
        controlCenterProbeCloseCount = [int]$manualFinished[0].control_center_probe_close_count
    }
    $manifest | Add-Member -NotePropertyName r4cMetrics -NotePropertyValue $r4cMetrics -Force
}
if ($manualFinished.Count -eq 1 -and [string]$manualFinished[0].manual_phase -eq 'R4D') {
    $r4dMetrics = [ordered]@{
        displayEnvironmentChangeCount = [int]$manualFinished[0].display_environment_change_count
        displayEnvironmentFailed = [bool]$manualFinished[0].display_environment_failed
    }
    $manifest | Add-Member -NotePropertyName r4dMetrics -NotePropertyValue $r4dMetrics -Force
}
if ($manualFinished.Count -eq 1 -and [string]$manualFinished[0].manual_phase -eq 'R4D2') {
    $r4d2Metrics = [ordered]@{
        themeSwitchCommittedCount = [int]$manualFinished[0].theme_switch_committed_count
        themeSwitchRejectedCount = [int]$manualFinished[0].theme_switch_rejected_count
        themeFallbackCount = [int]$manualFinished[0].theme_fallback_count
        themeSupersededRequestCount = [int]$manualFinished[0].theme_superseded_request_count
        themeSelfTestCompleted = [bool]$manualFinished[0].theme_self_test_completed
        selectedThemeId = [string]$manualFinished[0].selected_theme_id
        effectiveThemeId = [string]$manualFinished[0].effective_theme_id
        themeFallbackActive = [bool]$manualFinished[0].theme_fallback_active
        themeRevision = [long]$manualFinished[0].theme_revision
    }
    $manifest | Add-Member -NotePropertyName r4d2Metrics -NotePropertyValue $r4d2Metrics -Force
}if ($manualFinished.Count -eq 1 -and [string]$manualFinished[0].manual_phase -eq 'R4D3') {
    $r4d3Metrics = [ordered]@{
        recoveryCount = [int]$manualFinished[0].r4d3_recovery_count
        staleRejectCount = [int]$manualFinished[0].r4d3_stale_reject_count
        duplicateShellRejectCount = [int]$manualFinished[0].r4d3_duplicate_shell_reject_count
        stressCount = [int]$manualFinished[0].r4d3_stress_count
        maxThemeSwitchMs = [double]$manualFinished[0].r4d3_max_theme_switch_ms
        selfTestCompleted = [bool]$manualFinished[0].r4d3_self_test_completed
    }
    $manifest | Add-Member -NotePropertyName r4d3Metrics -NotePropertyValue $r4d3Metrics -Force
}
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
if ($null -ne $manifest.PSObject.Properties['r4cMetrics']) {
    Write-Output "R4C_SHELL_RECOVERY_COUNT=$($manifest.r4cMetrics.shellRecoveryCount)"
    Write-Output "R4C_SHELL_DEGRADED_COUNT=$($manifest.r4cMetrics.shellDegradedCount)"
    Write-Output "R4C_CONTROL_CENTER_OPEN_COUNT=$($manifest.r4cMetrics.controlCenterProbeOpenCount)"
    Write-Output "R4C_CONTROL_CENTER_CLOSE_COUNT=$($manifest.r4cMetrics.controlCenterProbeCloseCount)"
}
if ($null -ne $manifest.PSObject.Properties['r4dMetrics']) {
    Write-Output "R4D_DISPLAY_ENVIRONMENT_CHANGE_COUNT=$($manifest.r4dMetrics.displayEnvironmentChangeCount)"
    Write-Output "R4D_DISPLAY_ENVIRONMENT_FAILED=$($manifest.r4dMetrics.displayEnvironmentFailed)"
}
if ($null -ne $manifest.PSObject.Properties['r4d2Metrics']) {
    Write-Output "R4D2_THEME_SWITCH_COMMITTED_COUNT=$($manifest.r4d2Metrics.themeSwitchCommittedCount)"
    Write-Output "R4D2_THEME_SWITCH_REJECTED_COUNT=$($manifest.r4d2Metrics.themeSwitchRejectedCount)"
    Write-Output "R4D2_THEME_FALLBACK_COUNT=$($manifest.r4d2Metrics.themeFallbackCount)"
    Write-Output "R4D2_THEME_SUPERSEDED_REQUEST_COUNT=$($manifest.r4d2Metrics.themeSupersededRequestCount)"
    Write-Output "R4D2_THEME_SELF_TEST_COMPLETED=$($manifest.r4d2Metrics.themeSelfTestCompleted)"
    Write-Output "R4D2_EFFECTIVE_THEME_ID=$($manifest.r4d2Metrics.effectiveThemeId)"
}if ($null -ne $manifest.PSObject.Properties['r4d3Metrics']) {
    Write-Output "R4D3_RECOVERY_COUNT=$($manifest.r4d3Metrics.recoveryCount)"
    Write-Output "R4D3_STALE_REJECT_COUNT=$($manifest.r4d3Metrics.staleRejectCount)"
    Write-Output "R4D3_DUPLICATE_SHELL_REJECT_COUNT=$($manifest.r4d3Metrics.duplicateShellRejectCount)"
    Write-Output "R4D3_STRESS_COUNT=$($manifest.r4d3Metrics.stressCount)"
    Write-Output "R4D3_MAX_THEME_SWITCH_MS=$($manifest.r4d3Metrics.maxThemeSwitchMs)"
    Write-Output "R4D3_SELF_TEST_COMPLETED=$($manifest.r4d3Metrics.selfTestCompleted)"
}
Write-Output 'P0_RESULT=NOT_RUN'
