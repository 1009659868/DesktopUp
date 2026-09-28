#requires -Version 5.1

# Per-child environment works on both Windows PowerShell 5.1 and PowerShell 7.
# Arguments is the native command-line string; callers supply only fixed probe flags.
function Start-DesktopUpFixtureProcess {
    param(
        [Parameter(Mandatory)][string]$ExecutablePath,
        [Parameter(Mandatory)][string]$DataRoot,
        [string]$Arguments = '',
        [ValidateSet('Hidden','Normal')][string]$WindowStyle = 'Hidden',
        [switch]$MenuDiagnostics,
        [switch]$ContentFeedbackDiagnostics
    )
    $taskStart = New-Object System.Diagnostics.ProcessStartInfo
    $taskStart.FileName = $ExecutablePath
    $taskStart.WorkingDirectory = Split-Path -Parent $ExecutablePath
    $taskStart.UseShellExecute = $false
    $taskStart.Arguments = $Arguments
    $taskStart.WindowStyle = [System.Diagnostics.ProcessWindowStyle]$WindowStyle
    $taskStart.EnvironmentVariables['DESKTOPUP_V3_TEST_DATA_ROOT'] = $DataRoot
    $taskStart.EnvironmentVariables['DESKTOPUP_V3_MENU_DIAGNOSTICS'] = $(if ($MenuDiagnostics) { '1' } else { '0' })
    # Test-only poster footer/callout. Explicitly clear inherited opt-in for human review.
    $taskStart.EnvironmentVariables['DESKTOPUP_TEST_CONTENT_FEEDBACK'] = $(if ($ContentFeedbackDiagnostics) { '1' } else { '0' })
    # No RunAs/UAC change and no parent environment mutation.
    $taskStarted = [System.Diagnostics.Process]::Start($taskStart)
    if ($null -eq $taskStarted) { throw 'DesktopUp process creation returned no process.' }
    return $taskStarted
}
