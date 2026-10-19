#requires -Version 7
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [switch]$ResourceBaseline
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$rainApp = (Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$rainFixture = (Resolve-Path -LiteralPath $FixtureDirectory).Path.TrimEnd('\')
$rainOutput = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$rainAllowed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../out')).TrimEnd('\') + '\'
if (!$rainOutput.StartsWith($rainAllowed, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $rainOutput)) { throw 'Use a new output directory under repository out.' }
foreach ($rainCandidate in @($rainApp, $rainFixture)) {
    if ($rainOutput.StartsWith($rainCandidate + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Do not write into candidate artifacts.' }
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally; no process was stopped.' }
$rainDll = Join-Path $rainApp 'DesktopUp.dll'
if ((Get-FileHash -LiteralPath $rainDll).Hash -ine $ExpectedAssemblySha256) { throw 'Candidate hash mismatch.' }
function Get-RainArtifacts {
    foreach ($rainArea in @($rainApp, $rainFixture)) {
        Get-ChildItem -LiteralPath $rainArea -File -Recurse | Sort-Object FullName | ForEach-Object {
            [pscustomobject]@{ Path = $_.FullName; Length = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
        }
    }
}
$rainBinding = @(Get-RainArtifacts)
New-Item -ItemType Directory -Path $rainOutput | Out-Null
$rainBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rainOutput 'artifacts.json')
$rainData = Join-Path $rainOutput 'data'
if (!$ResourceBaseline) {
    & (Join-Path $rainFixture 'DesktopUp.SessionFixture.exe') $rainData 2
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed; evidence preserved.' }
}
$rainResults = @()
$rainModes = if ($ResourceBaseline) { @(1,3,6) } else { @('probe','restart') }
foreach ($rainMode in $rainModes) {
    if ($ResourceBaseline) {
        $rainData = Join-Path $rainOutput ('resources-' + $rainMode)
        & (Join-Path $rainFixture 'DesktopUp.SessionFixture.exe') $rainData $rainMode --measure-expanded
        if ($LASTEXITCODE -ne 0) { throw 'Resource fixture failed.' }
    }
    $rainFlag = if ($ResourceBaseline) { '--rain-resource-probe' } else { '--rain-scene-' + $rainMode }
    $rainArgs = $rainFlag + ' --exit-after-ms=90000 --animation-config="' + (Join-Path $rainApp 'config/poster-animation.json') + '"'
    $rainProcess = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $rainApp 'DesktopUp.exe') -DataRoot $rainData -Arguments $rainArgs -WindowStyle Hidden
    if (!$rainProcess.WaitForExit(95000)) { throw "Rain test timed out; process preserved: $($rainProcess.Id)" }
    $rainEvents = @(Get-Content -LiteralPath (Join-Path $rainData 'logs/desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object processId -eq $rainProcess.Id)
    $rainEvent = if ($ResourceBaseline) { 'rain.resources_observed' } else { 'rain.scene_observed' }
    $rainDone = @($rainEvents | Where-Object eventName -eq $rainEvent)
    $rainExit = @($rainEvents | Where-Object eventName -eq 'application.stopped')
    $rainErrors = @($rainEvents | Where-Object { $_.level -in @('error','critical','fatal') })
    $rainPassed = $rainProcess.ExitCode -eq 0 -and $rainDone.Count -eq 1 -and $rainDone[0].data.passed -and $rainExit.Count -eq 1 -and $rainExit[0].data.activeResources -eq 0 -and $rainErrors.Count -eq 0
    $rainResult = [pscustomobject]@{ Mode = $rainMode; Passed = $rainPassed; ProcessId = $rainProcess.Id; ExitCode = $rainProcess.ExitCode; Observation = $rainDone.data; Errors = $rainErrors; Cleanup = $rainExit.data }
    $rainResult | ConvertTo-Json -Depth 45 | Set-Content -LiteralPath (Join-Path $rainOutput ([string]$rainMode + '.json'))
    $rainResults += $rainResult
    if (!$rainPassed) { throw "Rain $rainMode failed; see $rainOutput" }
}
if (($rainBinding | ConvertTo-Json -Compress -Depth 5) -cne (@(Get-RainArtifacts) | ConvertTo-Json -Compress -Depth 5)) { throw 'Candidate changed during tests.' }
$rainResults | ConvertTo-Json -Depth 45 | Set-Content -LiteralPath (Join-Path $rainOutput 'results.json')
[pscustomobject]@{ Result = 'PASS - XAML/STATE ONLY'; Processes = $rainResults.ProcessId; DataRoot = $rainData; NativeInput = $false; DesktopPixelsCertified = $false }
