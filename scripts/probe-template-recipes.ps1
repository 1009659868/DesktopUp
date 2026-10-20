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
foreach ($taskProtected in @($taskApp, $taskFixture, $taskNormal)) {
    if ($taskOutput.Equals($taskProtected, [StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Recipe evidence must be isolated from the candidate artifacts and ordinary profile.'
    }
}
if (Test-Path -LiteralPath $taskOutput) { throw 'Preserve existing evidence and choose a fresh OutputRoot.' }
$taskExe = Join-Path $taskApp 'DesktopUp.exe'
$taskDll = Join-Path $taskApp 'DesktopUp.dll'
$taskFixtureExe = Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
foreach ($taskPath in @($taskExe, $taskDll, $taskFixtureExe)) {
    if (!(Test-Path -LiteralPath $taskPath -PathType Leaf)) { throw "Missing artifact: $taskPath" }
}
if ((Get-FileHash -LiteralPath $taskDll -Algorithm SHA256).Hash -ine $ExpectedAssemblySha256) { throw 'Selected assembly hash differs.' }
function Get-RecipeCandidateBinding {
    foreach ($taskArea in @('app','fixture')) {
        $taskFolder = if ($taskArea -eq 'app') { $taskApp } else { $taskFixture }
        foreach ($taskFile in (Get-ChildItem -LiteralPath $taskFolder -Recurse -File | Sort-Object FullName)) {
            [pscustomobject]@{ Path = $taskArea + '/' + $taskFile.FullName.Substring($taskFolder.Length).TrimStart('\').Replace('\','/');
                Length = $taskFile.Length; SHA256 = (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash }
        }
    }
    foreach ($taskName in @('probe-template-recipes.ps1','Start-DesktopUpFixtureProcess.ps1')) {
        $taskFile = Get-Item -LiteralPath (Join-Path $PSScriptRoot $taskName)
        [pscustomobject]@{ Path = 'scripts/' + $taskName; Length = $taskFile.Length;
            SHA256 = (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash }
    }
}
$taskBinding = @(Get-RecipeCandidateBinding)
if ($CheckOnly) {
    [pscustomobject]@{ Result = 'READY - NOT EXECUTED'; Artifacts = $taskBinding.Count; OutputRoot = $taskOutput;
        ExpectedAssemblySha256 = $ExpectedAssemblySha256; ContentFeedbackDiagnostics = $false }
    return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first; no process was stopped.' }
$null = New-Item -ItemType Directory -Path $taskOutput
$null = New-Item -ItemType Directory -Path (Join-Path $taskOutput 'scripts')
foreach ($taskName in @('probe-template-recipes.ps1','Start-DesktopUpFixtureProcess.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskName) -Destination (Join-Path $taskOutput ('scripts/' + $taskName))
}
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData = Join-Path $taskOutput 'data'
& $taskFixtureExe $taskData 2
if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed; partial evidence preserved.' }
function Invoke-RecipeProbe([string]$taskFlag) {
    $taskArguments = $taskFlag + ' --exit-after-ms=110000 --animation-config="' + (Join-Path $taskApp 'config\poster-animation.json') + '"'
    $taskProcess = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(115)
    while (!$taskProcess.WaitForExit(500)) {
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw "Recipe probe timed out; process preserved for normal exit: $($taskProcess.Id)" }
    }
    $taskEvents = @(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') -Encoding UTF8 |
        ForEach-Object { $_ | ConvertFrom-Json } | Where-Object processId -eq $taskProcess.Id)
    $taskDone = @($taskEvents | Where-Object eventName -eq 'template.recipe_observed')
    $taskStopped = @($taskEvents | Where-Object eventName -eq 'application.stopped')
    $taskErrors = @($taskEvents | Where-Object { $_.level -in @('error','critical','fatal') })
    $taskPassed = $taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskDone[0].data.passed -and
        $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0
    [pscustomobject]@{ Flag = $taskFlag; ProcessId = $taskProcess.Id; ExitCode = $taskProcess.ExitCode; Passed = $taskPassed;
        Observation = @($taskDone | ForEach-Object { $_.data }); Cleanup = @($taskStopped | ForEach-Object { $_.data }); Errors = $taskErrors }
}
$taskRuns = @()
$taskFirst = Invoke-RecipeProbe '--template-recipe-probe'
$taskRuns += $taskFirst
if ($taskFirst.Passed) { $taskRuns += Invoke-RecipeProbe '--template-recipe-restart' }
$taskStable = ($taskBinding | ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-RecipeCandidateBinding) | ConvertTo-Json -Depth 5 -Compress)
$taskPngs = @(Get-ChildItem -LiteralPath $taskData -Filter 'template-recipe-*.png' -File | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Name = $_.Name; Length = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$taskExpectedPngs = @('import-dialog','catalog-page','template-book','parameter-preview','save-copy-dialog','theme-parameters','restart-center') |
    ForEach-Object { 'template-recipe-' + $_ + '.png' }
$taskPassed = $taskStable -and $taskRuns.Count -eq 2 -and @($taskRuns | Where-Object { !$_.Passed }).Count -eq 0 -and
    $taskPngs.Count -eq $taskExpectedPngs.Count -and @(Compare-Object ($taskExpectedPngs | Sort-Object) ($taskPngs.Name | Sort-Object)).Count -eq 0 -and
    @($taskPngs | Where-Object Length -le 0).Count -eq 0
$taskResult = [pscustomobject]@{ Passed = $taskPassed; ExpectedAssemblySha256 = $ExpectedAssemblySha256; ArtifactsStable = $taskStable;
    Runs = $taskRuns; Pngs = $taskPngs; ProcessRestartTested = $taskPassed; ContentFeedbackDiagnostics = $false;
    ActualControlHandlers = $true; NativeMouse = $false; NativeKeyboard = $false; NativeFilePicker = $false;
    DesktopPixelVisualCertified = $false; CaptureIncludesSystemBackdrop = $false; UiCProductAccepted = $false; MilestoneAccepted = $false }
$taskResult | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if (!$taskPassed) { throw "Recipe integration observation failed; evidence preserved: $taskOutput" }
$taskResult | Select-Object Passed, ArtifactsStable, ProcessRestartTested, ContentFeedbackDiagnostics
