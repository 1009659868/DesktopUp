[CmdletBinding()]
param(
    [ValidateRange(5000, 900000)]
    [int]$DurationMs = 60000,

    [ValidatePattern('^[A-Za-z0-9_-]*$')]
    [string]$SessionLabel = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$candidateRoot = $PSScriptRoot
$outRoot = Join-Path $candidateRoot 'out'
$r3SummaryPath = Join-Path $outRoot 'r3-preflight.json'
if (-not (Test-Path -LiteralPath $r3SummaryPath -PathType Leaf)) {
    throw "R3 summary not found: $r3SummaryPath"
}

$r3 = Get-Content -Raw -LiteralPath $r3SummaryPath | ConvertFrom-Json
if ($r3.result -ne 'PASS' -or $r3.p0Result -ne 'NOT_RUN') {
    throw "R4 entry requires R3 PASS and P0 NOT_RUN. Current: result=$($r3.result), p0=$($r3.p0Result)"
}

$executable = [string]$r3.deployedExecutable
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Verified R3 deployed executable not found: $executable"
}
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $executable).Hash
$expectedHash = [string]$r3.deployedExecutableSha256
if ($actualHash -ne $expectedHash) {
    throw "R4 binary hash mismatch. Expected $expectedHash, got $actualHash"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$suffix = if ([string]::IsNullOrWhiteSpace($SessionLabel)) { '' } else { "-$SessionLabel" }
$sessionId = "r4-manual-$stamp$suffix"
$sessionRoot = Join-Path $outRoot $sessionId
New-Item -ItemType Directory -Path $sessionRoot | Out-Null

$eventLog = Join-Path $sessionRoot 'events.jsonl'
$readyMarker = Join-Path $sessionRoot 'matrix-ready.txt'
$manifestPath = Join-Path $sessionRoot 'session.json'
$arguments = @(
    '--run-r4-manual',
    '--r3-diagnostics',
    '--duration-ms', [string]$DurationMs,
    '--log', $eventLog,
    '--r3-ready-marker', $readyMarker
)

$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $executable) -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds(15)
while (-not (Test-Path -LiteralPath $readyMarker -PathType Leaf)) {
    if ($process.HasExited) {
        throw "R4 candidate exited before ready marker. ExitCode=$($process.ExitCode)"
    }
    if ([DateTime]::UtcNow -ge $deadline) {
        throw "R4 ready marker timeout after 15 seconds. PID=$($process.Id)"
    }
    Start-Sleep -Milliseconds 100
    $process.Refresh()
}

$manifest = [ordered]@{
    schema = 'desktopup.ts-c01.r4-manual-session.v1'
    candidateId = 'TS-C01'
    sessionId = $sessionId
    scope = 'R4_MANUAL_P0_IN_PROGRESS_NO_CANDIDATE_EVIDENCE'
    startedAt = (Get-Date).ToString('o')
    durationMs = $DurationMs
    processId = $process.Id
    executable = $executable
    executableSha256 = $actualHash
    r3Summary = $r3SummaryPath
    eventLog = $eventLog
    readyMarker = $readyMarker
    status = 'READY_FOR_MANUAL_OBSERVATION'
    p0Result = 'NOT_RUN'
    candidateEvidenceCreated = $false
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Output "R4_SESSION_READY=$sessionId"
Write-Output "PID=$($process.Id)"
Write-Output "EXE_SHA256=$actualHash"
Write-Output "EVENT_LOG=$eventLog"
Write-Output "SESSION_MANIFEST=$manifestPath"
Write-Output "VISIBLE_UNTIL_APPROX=$((Get-Date).AddMilliseconds($DurationMs).ToString('o'))"