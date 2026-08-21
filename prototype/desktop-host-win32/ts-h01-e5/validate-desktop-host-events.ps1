[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'
$expectedSchema = 'desktopup.desktop-host.event.v1'
$commonFields = @('schema', 'event', 'monotonic_ms', 'sequence', 'host_session_id', 'candidate_id', 'build_id', 'result')
$fencedEvents = @('attachment_state', 'projection_committed', 'surface_ready', 'frame_presented', 'surface_fault')
$fencedFields = @('attachment_id', 'shell_generation', 'lifecycle_epoch', 'surface_epoch', 'host_projection_revision', 'source_snapshot_revision', 'hit_region_revision', 'render_revision', 'display_environment_revision')
$errors = [System.Collections.Generic.List[string]]::new()
$events = [System.Collections.Generic.List[object]]::new()

function Has-Property([object]$Object, [string]$Name) {
    return $null -ne $Object.PSObject.Properties[$Name]
}

function Add-ValidationError([int]$LineNumber, [string]$Message) {
    $errors.Add("line $LineNumber`: $Message")
}

function Compact-Json([object]$Value) {
    if ($null -eq $Value) { return '<null>' }
    return ($Value | ConvertTo-Json -Depth 12 -Compress)
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    Write-Error "Event file not found: $Path"
    exit 2
}

$lineNumber = 0
foreach ($line in Get-Content -LiteralPath $Path) {
    $lineNumber++
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    try {
        $event = $line | ConvertFrom-Json -Depth 20
        $event | Add-Member -NotePropertyName '__line' -NotePropertyValue $lineNumber
        $events.Add($event)
    } catch {
        Add-ValidationError $lineNumber "invalid JSON: $($_.Exception.Message)"
    }
}

if ($events.Count -eq 0) {
    Add-ValidationError 0 'no JSON events found'
}

$expectedSequence = 1L
$lastMonotonic = -1L
$sessionIdentity = $null
$candidateIdentity = $null
$buildIdentity = $null
$sawExiting = $false
$sawEnded = $false
$sawZeroCleanup = $false
$latestProjection = @{}
$liveGenerationByAttachment = @{}

foreach ($event in $events) {
    $line = [int]$event.__line
    foreach ($field in $commonFields) {
        if (-not (Has-Property $event $field)) {
            Add-ValidationError $line "missing common field '$field'"
        }
    }
    if ((Has-Property $event 'schema') -and $event.schema -ne $expectedSchema) {
        Add-ValidationError $line "unexpected schema '$($event.schema)'"
    }

    if (Has-Property $event 'sequence') {
        if ([int64]$event.sequence -ne $expectedSequence) {
            Add-ValidationError $line "sequence must be $expectedSequence, got $($event.sequence)"
            $expectedSequence = [int64]$event.sequence
        }
        $expectedSequence++
    }
    if (Has-Property $event 'monotonic_ms') {
        if ([int64]$event.monotonic_ms -lt $lastMonotonic) {
            Add-ValidationError $line "monotonic_ms moved backwards from $lastMonotonic to $($event.monotonic_ms)"
        }
        $lastMonotonic = [int64]$event.monotonic_ms
    }

    if ($null -eq $sessionIdentity -and (Has-Property $event 'host_session_id')) { $sessionIdentity = [string]$event.host_session_id }
    if ($null -eq $candidateIdentity -and (Has-Property $event 'candidate_id')) { $candidateIdentity = [string]$event.candidate_id }
    if ($null -eq $buildIdentity -and (Has-Property $event 'build_id')) { $buildIdentity = [string]$event.build_id }
    if ((Has-Property $event 'host_session_id') -and [string]$event.host_session_id -ne $sessionIdentity) { Add-ValidationError $line 'host_session_id changed inside one stream' }
    if ((Has-Property $event 'candidate_id') -and [string]$event.candidate_id -ne $candidateIdentity) { Add-ValidationError $line 'candidate_id changed inside one stream' }
    if ((Has-Property $event 'build_id') -and [string]$event.build_id -ne $buildIdentity) { Add-ValidationError $line 'build_id changed inside one stream' }

    if ((Has-Property $event 'topmost') -and [bool]$event.topmost) { Add-ValidationError $line 'TopMost is forbidden' }
    if ((Has-Property $event 'ordinary_top_level_fallback') -and [bool]$event.ordinary_top_level_fallback) { Add-ValidationError $line 'ordinary top-level fallback is forbidden' }
    if ((Has-Property $event 'promoted_from') -and [string]$event.promoted_from -in @('UNKNOWN','NOT_RUN','DEFERRED','USER_ACCEPTED_WITH_EXCEPTION')) {
        Add-ValidationError $line "forbidden evidence promotion from $($event.promoted_from)"
    }

    if ([string]$event.event -in $fencedEvents) {
        foreach ($field in $fencedFields) {
            if (-not (Has-Property $event $field)) { Add-ValidationError $line "missing fenced field '$field'" }
        }
    }

    if ([string]$event.event -eq 'projection_committed' -and (Has-Property $event 'attachment_id')) {
        $latestProjection[[string]$event.attachment_id] = $event
    }

    if ([string]$event.event -eq 'attachment_state' -and (Has-Property $event 'attachment_id')) {
        $attachment = [string]$event.attachment_id
        if ((Has-Property $event 'host_state') -and [string]$event.host_state -eq 'ATTACHED_SAFE') {
            $generation = [string]$event.shell_generation
            foreach ($pair in @($liveGenerationByAttachment.GetEnumerator())) {
                if ($pair.Key -ne $attachment -and [string]$pair.Value -eq $generation) {
                    Add-ValidationError $line "multiple live attachments for shell_generation $generation"
                }
            }
            $liveGenerationByAttachment[$attachment] = $generation
        } elseif ((Has-Property $event 'host_state') -and [string]$event.host_state -in @('DETACHED','DETACHING','SHELL_GENERATION_LOST','DEGRADED_NO_DESKTOP_HOST','EXITING')) {
            $liveGenerationByAttachment.Remove($attachment)
        }
    }

    if ([string]$event.event -in @('surface_ready','frame_presented')) {
        $attachment = [string]$event.attachment_id
        if (-not $latestProjection.ContainsKey($attachment)) {
            Add-ValidationError $line "$($event.event) has no preceding projection_committed"
        } else {
            $projection = $latestProjection[$attachment]
            foreach ($field in @('shell_generation','lifecycle_epoch','surface_epoch','host_projection_revision','source_snapshot_revision','hit_region_revision','render_revision','display_environment_revision')) {
                if ((Has-Property $event $field) -and (Has-Property $projection $field) -and [string]$event.$field -ne [string]$projection.$field) {
                    Add-ValidationError $line "$($event.event) stale/mismatched ${field}: expected $($projection.$field), got $($event.$field)"
                }
            }
            if ((Has-Property $event 'physical_bounds') -and (Has-Property $event.physical_bounds 'render_target') -and
                (Has-Property $projection 'physical_bounds') -and (Has-Property $projection.physical_bounds 'render_target')) {
                $actualBounds = Compact-Json $event.physical_bounds.render_target
                $expectedBounds = Compact-Json $projection.physical_bounds.render_target
                if ($actualBounds -ne $expectedBounds) { Add-ValidationError $line "$($event.event) render_target bounds mismatch" }
            }
        }
    }

    if ([string]$event.event -eq 'host_session_exiting') { $sawExiting = $true }
    if ([string]$event.event -eq 'cleanup_completed') {
        if (Has-Property $event 'resource_counts') {
            $counts = $event.resource_counts
            $sawZeroCleanup = ([int]$counts.host -eq 0 -and [int]$counts.adapter -eq 0 -and [int]$counts.quarantine -eq 0)
            if (-not $sawZeroCleanup) { Add-ValidationError $line 'cleanup_completed resource counts are not zero' }
        } else { Add-ValidationError $line 'cleanup_completed missing resource_counts' }
    }
    if ([string]$event.event -eq 'host_session_ended') {
        $sawEnded = $true
        if (-not $sawExiting) { Add-ValidationError $line 'host_session_ended occurred before host_session_exiting' }
        if (-not (Has-Property $event 'resource_counts')) { Add-ValidationError $line 'host_session_ended missing resource_counts' }
        else {
            $counts = $event.resource_counts
            if ([int]$counts.host -ne 0 -or [int]$counts.adapter -ne 0 -or [int]$counts.quarantine -ne 0) {
                Add-ValidationError $line 'host_session_ended resource counts are not zero'
            }
        }
    }
}

if (-not $sawEnded) { Add-ValidationError 0 'missing host_session_ended' }
if (-not $sawZeroCleanup) { Add-ValidationError 0 'missing successful zero-resource cleanup_completed' }

if ($errors.Count -gt 0) {
    Write-Host "FAIL: $($errors.Count) conformance error(s) in $Path"
    foreach ($errorMessage in $errors) { Write-Host "  - $errorMessage" }
    exit 1
}

Write-Host "PASS: $($events.Count) events conform to $expectedSchema"
exit 0
