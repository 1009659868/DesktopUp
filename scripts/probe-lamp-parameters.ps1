#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp = (Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskFixture = (Resolve-Path -LiteralPath $FixtureDirectory).Path.TrimEnd('\')
$taskOutput = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach ($taskProtected in @($taskNormal, $taskApp, $taskFixture)) {
    if ($taskOutput.Equals($taskProtected, [StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Lamp probe output must be isolated from the normal profile and candidate artifacts.'
    }
}
if (Test-Path -LiteralPath $taskOutput) { throw 'OutputRoot already exists; preserve it and use a new directory.' }
$taskExecutable = Join-Path $taskApp 'DesktopUp.exe'
$taskAssembly = Join-Path $taskApp 'DesktopUp.dll'
$taskFixtureExecutable = Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
foreach ($taskPath in @($taskExecutable, $taskAssembly, $taskFixtureExecutable)) {
    if (!(Test-Path -LiteralPath $taskPath -PathType Leaf)) { throw "Missing executable: $taskPath" }
}
if ((Get-FileHash -LiteralPath $taskAssembly -Algorithm SHA256).Hash -ine $ExpectedAssemblySha256) {
    throw 'DesktopUp.dll does not match the explicitly selected candidate code hash.'
}
function Get-LampCandidateBinding {
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
$taskBinding = @(Get-LampCandidateBinding)
if ($CheckOnly) {
    [pscustomobject]@{ Result = 'READY - NOT EXECUTED'; OutputRoot = $taskOutput; Artifacts = $taskBinding.Count; ExpectedAssemblySha256 = $ExpectedAssemblySha256 }
    return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) {
    throw 'Exit existing DesktopUp normally first. No process was stopped.'
}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData = Join-Path $taskOutput 'data'
# The ordinary 2-poster fixture starts both expanded. Measurement mode only accepts 1/3/6.
& $taskFixtureExecutable $taskData 2
if ($LASTEXITCODE -ne 0) { throw 'Fixture creation failed; partial evidence preserved.' }
$taskArguments = '--lamp-parameter-probe --exit-after-ms=85000 --animation-config="' + (Join-Path $taskApp 'config\poster-animation.json') + '"'
$taskProcess = Start-DesktopUpFixtureProcess -ExecutablePath $taskExecutable -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskDeadline = [DateTime]::UtcNow.AddSeconds(90)
while (!$taskProcess.WaitForExit(500)) {
    if ([DateTime]::UtcNow -gt $taskDeadline) { throw "Lamp probe timed out; process preserved for normal exit: $($taskProcess.Id)" }
}
$taskLog = Join-Path $taskData 'logs\desktopup.jsonl'
if (!(Test-Path -LiteralPath $taskLog)) { throw "No process log produced: $taskData" }
$taskEvents = @(Get-Content -LiteralPath $taskLog -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json } |
    Where-Object { $_.processId -eq $taskProcess.Id })
$taskDone = @($taskEvents | Where-Object eventName -eq 'lamp.parameters_observed')
$taskStopped = @($taskEvents | Where-Object eventName -eq 'application.stopped')
$taskErrors = @($taskEvents | Where-Object { $_.level -in @('error','critical','fatal') })
$taskStable = ($taskBinding | ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-LampCandidateBinding) | ConvertTo-Json -Depth 5 -Compress)
$taskPngs = @(Get-ChildItem -LiteralPath $taskData -Filter 'lamp-*.png' -File | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Name = $_.Name; Length = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$taskExpectedPngs = @('preview','transparent','second-poster','color-picker','title-ink','title-hidden') | ForEach-Object {
    'lamp-'+$_+'-center.png'; 'lamp-'+$_+'-poster-xaml.png'
}
$taskExpectedPngs += 'lamp-color-picker-popup.png'
$taskMotionCases = @('real-shared-motion-frames','editing-stops-shared-motion','per-poster-motion-disabled-preview',
    'global-pause-stops-shared-motion','all-collapsed-motion-resources-zero','environment-suspend-motion-resources-zero',
    'scene-host-failure-detaches-only-decoration','transparent-title-colors-preview-cancel-single-commit-peer-isolation',
    'title-visibility-immediate-single-commit-keeps-name-geometry-and-peer')
$taskMotionObserved = $false
if ($taskDone.Count -eq 1) {
    $taskObservedNames = @($taskDone[0].data.observations | ForEach-Object { $_.name })
    $taskMotionObserved = $taskDone[0].data.pointerFollowingImplemented -eq $true -and
        @($taskMotionCases | Where-Object { $_ -notin $taskObservedNames }).Count -eq 0
    $taskFrameObservation = @($taskDone[0].data.observations | Where-Object name -eq 'real-shared-motion-frames')
    if ($taskFrameObservation.Count -ne 1) { $taskMotionObserved = $false }
    elseif (!$taskFrameObservation[0].Scene.ReducedMotion -and !$taskFrameObservation[0].Scene.GlobalMotionPaused) {
        $taskMotionObserved = $taskMotionObserved -and $taskDone[0].data.timerAdvanceObserved -eq $true -and
            $taskDone[0].data.pointerHandlerObserved -eq $true -and $taskDone[0].data.idleHandlerObserved -eq $true
        $taskMotionObserved = $taskMotionObserved -and 'held-item-and-refresh-keep-lamp-following' -in $taskObservedNames
    }
}
$taskPassed = $taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskDone[0].data.passed -and
    $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0 -and
    $taskStable -and $taskMotionObserved -and $taskPngs.Count -eq $taskExpectedPngs.Count -and
    @(Compare-Object ($taskExpectedPngs | Sort-Object) ($taskPngs.Name | Sort-Object)).Count -eq 0 -and
    @($taskPngs | Where-Object Length -le 0).Count -eq 0
$taskResult = [pscustomobject]@{
    Result = $(if ($taskPassed) { 'OBSERVED - XAML/STATE ONLY' } else { 'FAILED - EVIDENCE PRESERVED' })
    ProcessId = $taskProcess.Id; ExitCode = $taskProcess.ExitCode; DataRoot = $taskData
    ExpectedAssemblySha256 = $ExpectedAssemblySha256; ArtifactsStable = $taskStable; Pngs = $taskPngs
    Observation = @($taskDone | ForEach-Object { $_.data }); Errors = $taskErrors
    Cleanup = @($taskStopped | ForEach-Object { $_.data })
    NativeMouse = $false; NativeKeyboard = $false; NativeWheel = $false
    MotionCasesObserved = $taskMotionObserved; MotionCasesRequired = $taskMotionCases
    DesktopPixelVisualCertified = $false; CaptureIncludesSystemBackdrop = $false
    ProcessRestartTested = $false; UiCProductAccepted = $false; MilestoneAccepted = $false
}
$taskResult | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if (!$taskPassed) { throw "Lamp observation failed; inspect $(Join-Path $taskOutput 'observation.json')" }
$taskResult | Select-Object Result, ProcessId, ExitCode, DataRoot, ArtifactsStable, UiCProductAccepted
