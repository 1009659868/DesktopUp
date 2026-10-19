#requires -Version 5.1
param([ValidateSet('three','one','empty','constrained')][string]$Scenario='three', [switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=Join-Path $taskRepo 'out\verify\v3-04-04-06'
$taskManifest=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'control-center-candidate.json') | ConvertFrom-Json
$taskChecked=0
foreach ($taskArea in $taskManifest.PSObject.Properties) {
    foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
        $taskFile=Join-Path (Join-Path $taskPackage $taskArea.Name) $taskEntry.Name
        if (!(Test-Path -LiteralPath $taskFile -PathType Leaf) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
            throw "Control center candidate missing/changed: $taskFile. No fallback to an older build."
        }
        $taskChecked++
    }
}
if ($taskChecked -ne 25) { throw 'Expected 25 bound artifacts/configuration files.' }
$taskData=Join-Path $taskRepo "out\manual\v3-04-04\$Scenario"
if ($CheckOnly) { [pscustomobject]@{Result='CANDIDATE CHECKED - MANUAL PENDING'; Scenario=$Scenario; DataRoot=$taskData; VerifiedArtifacts=$taskChecked}; return }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Please exit the existing DesktopUp normally from its tray first.' }
if (!(Test-Path -LiteralPath $taskData)) {
    $taskCount=switch($Scenario) { 'empty' {0} 'one' {1} default {3} }
    if ($Scenario -eq 'empty') { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount | Out-Null }
    else {
        $taskMode=if ($Scenario -eq 'constrained') {'--r2-constrained'} else {'--measure-expanded'}
        & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount $taskMode | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed.' }
} elseif (!(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)) {
    throw "Incomplete profile preserved, please inspect: $taskData"
}
$taskArguments='--show-control-center --animation-config="' + (Join-Path $taskPackage 'app\config\poster-animation.json') + '"'
$taskStarted=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - V3-04-04-06 MANUAL PENDING'; Scenario=$Scenario; ProcessId=$taskStarted.Id; DataRoot=$taskData;
    LogPath=(Join-Path $taskData 'logs\desktopup.jsonl'); AppHash=$taskManifest.app.'DesktopUp.dll';
    Scope='Isolated auto-OFF profile; same command preserves edits across restart; no smoke/input injection; no ordinary data modified'}
