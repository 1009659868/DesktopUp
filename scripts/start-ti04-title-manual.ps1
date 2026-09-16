#requires -Version 5.1
param([ValidateSet('basic','chain','constrained','blocked')][string]$Scenario='chain',
    [switch]$CheckOnly, [ValidateRange(0,60000)][int]$ExitAfterMs=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=Join-Path $taskRepo 'out\verify\v3-04-m08-07-ti04-safe-stop'
$taskManifest=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'ti04-candidate.json') | ConvertFrom-Json
$taskCount=0
foreach ($taskArea in $taskManifest.PSObject.Properties) {
    foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
        $taskFile=Join-Path (Join-Path $taskPackage $taskArea.Name) $taskEntry.Name
        if (!(Test-Path -LiteralPath $taskFile -PathType Leaf) -or
            (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
            throw "TI-04 candidate missing/changed: $taskFile. No fallback to R1."
        }
        $taskCount++
    }
}
if ($taskCount -ne 25) { throw 'Expected 25 bound candidate artifacts/config files.' }
# Candidate diagnostics only: Get-M08Build.ps1 / m08-build.json remain bound to R1.
# A fresh profile keeps the original failed candidate's geometry/logs intact for comparison.
$taskData=Join-Path $taskRepo "out\manual\v3-04-m08-07-ti04-safe-stop\$Scenario"
$taskExists=Test-Path -LiteralPath $taskData
if ($taskExists -and !(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)) {
    throw "Incomplete fixture preserved: $taskData"
}
if ($CheckOnly) {
    [pscustomobject]@{ Result='TI-04 CANDIDATE CHECKED - NATIVE TITLE GATES PENDING'; Scenario=$Scenario;
        VerifiedArtifacts=$taskCount; DataRoot=$taskData; Launched=$false }; return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally first.' }
if (!$taskExists) {
    $taskMode=switch($Scenario) {
        'chain' { '--spatial-chain-r2' }
        'constrained' { '--r2-constrained' }
        'blocked' { '--r2-blocked' }
        default { '--measure-expanded' }
    }
    & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData 3 $taskMode | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed.' }
}
$taskConfig=Join-Path $taskPackage 'app\config\poster-animation.json'
$taskArguments='--show-control-center --animation-config="' + $taskConfig + '"'
if ($ExitAfterMs -gt 0) { $taskArguments += " --exit-after-ms=$ExitAfterMs" }
$taskProbe=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{ Result='TI-04 CANDIDATE - MANUAL PENDING'; Scenario=$Scenario; ProcessId=$taskProbe.Id;
    DataRoot=$taskData; LogPath=(Join-Path $taskData 'logs\desktopup.jsonl'); AppHash=$taskManifest.app.'DesktopUp.dll';
    AutoEnabled='Use control center to enable per poster; no smoke or pointer injection'; AutoExitAfterMs=$ExitAfterMs }
