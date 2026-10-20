#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp = (Resolve-Path -LiteralPath $AppDirectory).Path
$taskFixture = (Resolve-Path -LiteralPath $FixtureDirectory).Path
$taskOutput = [IO.Path]::GetFullPath($OutputRoot)
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskOutput.Equals($taskNormal, [StringComparison]::OrdinalIgnoreCase) -or
    $taskOutput.StartsWith($taskNormal + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'UI C sample must use a new isolated output directory outside the normal profile.'
}
if (Test-Path -LiteralPath $taskOutput) { throw 'OutputRoot already exists; preserve it and use a new directory.' }
$taskExecutable = Join-Path $taskApp 'DesktopUp.exe'
$taskFixtureExecutable = Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
foreach ($taskPath in @($taskExecutable, $taskFixtureExecutable)) {
    if (!(Test-Path -LiteralPath $taskPath -PathType Leaf)) { throw "Missing executable: $taskPath" }
}
function Get-UiCSampleBinding {
    $taskRows = @()
    foreach ($taskArea in @('app','fixture')) {
        $taskDirectory = if ($taskArea -eq 'app') { $taskApp } else { $taskFixture }
        foreach ($taskFile in (Get-ChildItem -LiteralPath $taskDirectory -File -Recurse | Sort-Object FullName)) {
            $taskRelative = $taskFile.FullName.Substring($taskDirectory.Length).TrimStart('\').Replace('\','/')
            $taskRows += [pscustomobject]@{ Path = $taskArea + '/' + $taskRelative; Length = $taskFile.Length;
                SHA256 = (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash }
        }
    }
    return $taskRows
}
$taskBinding = @(Get-UiCSampleBinding)
if ($CheckOnly) {
    [pscustomobject]@{ Result = 'READY - NOT EXECUTED'; OutputRoot = $taskOutput; Artifacts = $taskBinding.Count }
    return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) {
    throw 'Exit existing DesktopUp normally first. No process was stopped.'
}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData = Join-Path $taskOutput 'data'
& $taskFixtureExecutable $taskData 1 --measure-expanded
if ($LASTEXITCODE -ne 0) { throw 'Fixture creation failed; partial data preserved.' }
$taskArguments = '--ui-c-sample-probe --exit-after-ms=40000 --animation-config="' + (Join-Path $taskApp 'config\poster-animation.json') + '"'
$taskProcess = Start-DesktopUpFixtureProcess -ExecutablePath $taskExecutable -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskDeadline = [DateTime]::UtcNow.AddSeconds(45)
while (!$taskProcess.WaitForExit(500)) {
    if ([DateTime]::UtcNow -gt $taskDeadline) { throw "UI C probe timed out; process preserved for normal exit: $($taskProcess.Id)" }
}
$taskLog = Join-Path $taskData 'logs\desktopup.jsonl'
if (!(Test-Path -LiteralPath $taskLog)) { throw "No process log produced: $taskData" }
$taskEvents = @(Get-Content -LiteralPath $taskLog | ForEach-Object { $_ | ConvertFrom-Json } |
    Where-Object { $_.processId -eq $taskProcess.Id })
$taskDone = @($taskEvents | Where-Object eventName -eq 'ui_c.sample_observed')
$taskStopped = @($taskEvents | Where-Object eventName -eq 'application.stopped')
$taskErrors = @($taskEvents | Where-Object { $_.level -in @('error','critical','fatal') })
$taskStable = ($taskBinding | ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-UiCSampleBinding) | ConvertTo-Json -Depth 5 -Compress)
$taskPassed = $taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskDone[0].data.passed -and
    $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0 -and $taskStable
$taskResult = [pscustomobject]@{
    Result = $(if ($taskPassed) { 'OBSERVED - XAML/CANVAS ONLY' } else { 'FAILED - EVIDENCE PRESERVED' })
    ProcessId = $taskProcess.Id; ExitCode = $taskProcess.ExitCode; DataRoot = $taskData
    ArtifactsStable = $taskStable; Observation = @($taskDone | ForEach-Object { $_.data })
    Errors = $taskErrors; NativeMouse = $false; NativeKeyboard = $false; NativeWheel = $false
    DesktopPixelVisualCertified = $false; UiCProductAccepted = $false
}
$taskResult | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if (!$taskPassed) { throw "UI C observation failed; inspect $(Join-Path $taskOutput 'observation.json')" }
$taskResult | Select-Object Result, ProcessId, ExitCode, DataRoot, ArtifactsStable, UiCProductAccepted
