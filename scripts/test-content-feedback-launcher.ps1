#requires -Version 5.1
param([Parameter(Mandatory)][string]$OutputRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskOutput = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $taskOutput) { throw 'Preserve existing test evidence and choose a fresh OutputRoot.' }
$null = New-Item -ItemType Directory -Path $taskOutput
$taskChildScript = Join-Path $taskOutput 'read-child-environment.ps1'
@'
param([string]$ResultPath)
[pscustomobject]@{
    Feedback = [Environment]::GetEnvironmentVariable('DESKTOPUP_TEST_CONTENT_FEEDBACK', 'Process')
    DataRoot = [Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT', 'Process')
} | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding UTF8
'@ | Set-Content -LiteralPath $taskChildScript -Encoding UTF8
$taskShell = (Get-Process -Id $PID).Path
$taskParentFeedback = [Environment]::GetEnvironmentVariable('DESKTOPUP_TEST_CONTENT_FEEDBACK', 'Process')
$taskParentData = [Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT', 'Process')
$taskResults = @()
try {
    foreach ($taskInherited in @('0','1')) {
        [Environment]::SetEnvironmentVariable('DESKTOPUP_TEST_CONTENT_FEEDBACK', $taskInherited, 'Process')
        foreach ($taskOptIn in @($false,$true)) {
            $taskCase = 'inherited-' + $taskInherited + '-opt-in-' + $taskOptIn.ToString().ToLowerInvariant()
            $taskResultPath = Join-Path $taskOutput ($taskCase + '.json')
            $taskData = Join-Path $taskOutput ($taskCase + '-unused-data')
            $taskArguments = '-NoProfile -File "' + $taskChildScript + '" -ResultPath "' + $taskResultPath + '"'
            $taskChild = Start-DesktopUpFixtureProcess -ExecutablePath $taskShell -DataRoot $taskData -Arguments $taskArguments -ContentFeedbackDiagnostics:$taskOptIn
            try {
                if (!$taskChild.WaitForExit(15000)) { throw "Environment reader timed out; child preserved: $($taskChild.Id)" }
                if ($taskChild.ExitCode -ne 0) { throw 'Environment reader failed.' }
            } finally { $taskChild.Dispose() }
            $taskRead = Get-Content -LiteralPath $taskResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $taskExpected = if ($taskOptIn) { '1' } else { '0' }
            $taskPassed = $taskRead.Feedback -ceq $taskExpected -and $taskRead.DataRoot -ceq $taskData -and
                [Environment]::GetEnvironmentVariable('DESKTOPUP_TEST_CONTENT_FEEDBACK', 'Process') -ceq $taskInherited -and
                [Environment]::GetEnvironmentVariable('DESKTOPUP_V3_TEST_DATA_ROOT', 'Process') -ceq $taskParentData
            $taskResults += [pscustomobject]@{ Case = $taskCase; Passed = $taskPassed; Expected = $taskExpected; Actual = $taskRead.Feedback }
            if (!$taskPassed) { throw "Child feedback opt-in isolation failed: $taskCase" }
        }
    }
} finally {
    [Environment]::SetEnvironmentVariable('DESKTOPUP_TEST_CONTENT_FEEDBACK', $taskParentFeedback, 'Process')
}
$taskReport = [pscustomobject]@{ Passed = $true; PowerShell = $PSVersionTable.PSVersion.ToString(); Cases = $taskResults;
    DesktopUpStarted = $false; Scope = 'Actual child-process opt-in isolation; no application, fixture, or UI is started.' }
$taskReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'result.json') -Encoding UTF8
$taskReport | Select-Object Passed, PowerShell, DesktopUpStarted
