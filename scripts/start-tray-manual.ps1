#requires -Version 5.1
param([ValidateSet('three','one','empty')][string]$Scenario='three', [switch]$CheckOnly,
    [ValidateSet('global','layout')][string]$Candidate='global')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=Join-Path $taskRepo ('out\verify\v3-04-05-' + $Candidate)
$taskManifestName=if ($Candidate -eq 'layout') { 'tray-layout-candidate.json' } else { 'tray-candidate.json' }
$taskManifest=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $taskManifestName) | ConvertFrom-Json
$taskChecked=0
foreach ($taskArea in $taskManifest.PSObject.Properties) {
    foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
        $taskFile=Join-Path (Join-Path $taskPackage $taskArea.Name) $taskEntry.Name
        if (!(Test-Path -LiteralPath $taskFile -PathType Leaf) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
            throw "Tray candidate missing/changed: $taskFile. No fallback to an older build."
        }
        $taskChecked++
    }
}
if ($taskChecked -ne 25) { throw 'Expected 25 bound artifacts/configuration files.' }
# PS5.1 emits a JSON array as one pipeline object; wrapping the pipeline in @() nests it.
$taskGates=Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'gates-passed.json') | ConvertFrom-Json
$taskKeys=@($taskGates | ForEach-Object { "$($_.Mode)/$($_.Posters)" } | Sort-Object -Unique)
$taskExpected=@(foreach($taskMode in @('exercise','restart','controls','settings')) { foreach($taskN in @(0,1,3)) { "$taskMode/$taskN" } })
$taskKeyDifferences=@(Compare-Object ($taskExpected | Sort-Object) $taskKeys)
$taskInvalidGates=@($taskGates | Where-Object { $_.Result -ne 'PASS - AUTOMATED ONLY' -or $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or
        $_.FixtureHash -ne $taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or
        $_.AnimationHash -ne $taskManifest.app.'config/poster-animation.json' -or $_.ResourcesAfterExit -ne 0 })
if ($taskGates.Count -ne 12 -or $taskKeyDifferences.Count -ne 0 -or $taskInvalidGates.Count -ne 0) {
    throw "The twelve same-candidate gates are not complete: count=$($taskGates.Count), key-differences=$($taskKeyDifferences.Count), invalid=$($taskInvalidGates.Count), PS=$($PSVersionTable.PSVersion)."
}
$taskData=Join-Path $taskRepo ("out\manual\v3-04-05-" + $Candidate + '\' + $Scenario)
if ($CheckOnly) {
    [pscustomobject]@{Result='CANDIDATE CHECKED - MANUAL PENDING'; Scenario=$Scenario; DataRoot=$taskData; VerifiedArtifacts=$taskChecked}; return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Please exit existing DesktopUp normally from its tray first.' }
if (!(Test-Path -LiteralPath $taskData)) {
    $taskCount=switch($Scenario) { 'empty' {0} 'one' {1} default {3} }
    if ($taskCount -eq 0) { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData 0 | Out-Null }
    else { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount --measure-expanded | Out-Null }
    if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed.' }
} elseif (!(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)) {
    throw "Incomplete profile preserved, please inspect: $taskData"
}
$taskArguments='--animation-config="' + (Join-Path $taskPackage 'app\config\poster-animation.json') + '"'
$taskStarted=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - V3-04-05 MANUAL PENDING'; Scenario=$Scenario; ProcessId=$taskStarted.Id; DataRoot=$taskData;
    LogPath=(Join-Path $taskData 'logs\desktopup.jsonl'); AppHash=$taskManifest.app.'DesktopUp.dll';
    Scope='Starts in tray; isolated auto-OFF profile; repeated command preserves edits; no ordinary data changed'}
