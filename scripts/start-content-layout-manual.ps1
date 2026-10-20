#requires -Version 5.1
param([ValidateSet('equal','illustration','covers','landscape','three','empty','hundred','thousand','resource-one','resource-three','resource-six')]
    [string]$Scenario='three',[switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=Join-Path $taskRepo 'out\verify\v3-04-l-r4'
$taskManifestPath=Join-Path $PSScriptRoot 'content-layout-candidate.json'
if(!(Test-Path -LiteralPath $taskManifestPath -PathType Leaf)){throw 'Content candidate is not sealed yet. No fallback to an older package.'}
$taskManifest=Get-Content -Raw -LiteralPath $taskManifestPath | ConvertFrom-Json
if(@($taskManifest.PSObject.Properties | Where-Object Name -NotIn @('app','fixture')).Count){throw 'Unexpected candidate group.'}
$taskRows=@();$taskChecked=0
foreach($taskArea in @('app','fixture')){
    foreach($taskEntry in ($taskManifest.$taskArea.PSObject.Properties | Sort-Object Name)){
        if($taskEntry.Name -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($taskEntry.Name) -or
            $taskEntry.Value -notmatch '^[0-9A-Fa-f]{64}$'){throw 'Invalid content candidate artifact descriptor.'}
        $taskPath=Join-Path (Join-Path $taskPackage $taskArea) $taskEntry.Name
        if(!(Test-Path -LiteralPath $taskPath -PathType Leaf) -or (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskEntry.Value){
            throw "Content candidate artifact changed/missing: $taskPath. No fallback."
        }
        $taskRows+=($taskArea+'/'+$taskEntry.Name+'='+$taskEntry.Value.ToUpperInvariant());$taskChecked++
    }
}
if($taskChecked -ne 25 -or !$taskManifest.app.'DesktopUp.exe' -or !$taskManifest.app.'DesktopUp.dll' -or
    !$taskManifest.app.'config/poster-animation.json' -or !$taskManifest.fixture.'DesktopUp.SessionFixture.dll'){
    throw 'All 25 candidate binaries/runtime/config artifacts must be bound.'
}
$taskSha=[Security.Cryptography.SHA256]::Create()
try{$taskFingerprint=([BitConverter]::ToString($taskSha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($taskRows -join "`n"))))).Replace('-','')}
finally{$taskSha.Dispose()}
$taskGates=Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'content-layout-gates-passed.json') | ConvertFrom-Json
$taskCases=@('empty','hundred','resource-one','resource-six','resource-three','thousand','three')
$taskExpected=@(foreach($taskCase in $taskCases){"$taskCase/False";"$taskCase/True"})
$taskActual=@($taskGates | ForEach-Object{"$($_.Scenario)/$([bool]$_.Restart)"} | Sort-Object -Unique)
$taskInvalid=@($taskGates | Where-Object{
    $_.Result -ne 'PASS - PRODUCTION COMMAND/CANVAS ONLY' -or $_.ArtifactFingerprint -ne $taskFingerprint -or
    $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or $_.FixtureHash -ne $taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or
    $_.ResourcesAfterExit -ne 0 -or $_.ExitCode -ne 0 -or $_.ResourceSamples -ne 12 -or $_.SampledSeconds -lt 10 -or $_.Checks -lt 10 -or
    $_.NativeMouse -ne $false -or $_.NativeKeyboard -ne $false -or $_.NativeWheel -ne $false
})
if($taskGates.Count -ne 14 -or $taskInvalid.Count -or (Compare-Object ($taskExpected | Sort-Object) $taskActual)){
    throw 'Fourteen same-package content process/restart gates are incomplete or invalid.'
}
$taskRegression=Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'gates-passed.json') | ConvertFrom-Json
$taskExpected=@(foreach($taskCount in @(0,1,3)){foreach($taskMode in @('exercise','restart','controls','settings')){"$taskCount/$taskMode"}})
$taskActual=@($taskRegression | ForEach-Object{"$($_.Posters)/$($_.Mode)"} | Sort-Object -Unique)
if($taskRegression.Count -ne 12 -or (Compare-Object ($taskExpected | Sort-Object) $taskActual) -or
    @($taskRegression | Where-Object { $_.Result -ne 'PASS - AUTOMATED ONLY' -or $_.ResourcesAfterExit -ne 0 -or
        $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or $_.FixtureHash -ne $taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or
        $_.AnimationHash -ne $taskManifest.app.'config/poster-animation.json' -or $_.NativeMouse -ne $false }).Count){
    throw 'Twelve same-package tray/control/settings gates are incomplete or invalid.'
}
$taskPortable=Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'portable-gates-passed.json') | ConvertFrom-Json
$taskExpected=@(foreach($taskCount in @(0,1,3)){"$taskCount/False";"$taskCount/True"})
$taskActual=@($taskPortable | ForEach-Object{"$($_.Posters)/$([bool]$_.Restart)"} | Sort-Object -Unique)
if($taskPortable.Count -ne 6 -or (Compare-Object ($taskExpected | Sort-Object) $taskActual) -or
    @($taskPortable | Where-Object { $_.Result -ne 'PASS - AUTOMATED CONTROLLER/RUNTIME ONLY' -or $_.ResourcesAfterExit -ne 0 -or
        $_.ExitCode -ne 0 -or $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or $_.AppExeHash -ne $taskManifest.app.'DesktopUp.exe' -or
        $_.FixtureHash -ne $taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or $_.NativeMouse -ne $false -or
        $_.NativePicker -ne $false -or $_.RealCrossHost -ne $false }).Count){
    throw 'Six same-package portable process/restart gates are incomplete or invalid.'
}
$taskRecovery=Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'recovery-gates-passed.json') | ConvertFrom-Json
$taskActual=@($taskRecovery | ForEach-Object{$_.Case} | Sort-Object -Unique)
if($taskRecovery.Count -ne 3 -or (Compare-Object @('corrupt','healthy','pending') $taskActual) -or
    @($taskRecovery | Where-Object { $_.Result -ne 'PASS - OFFLINE WINDOW ONLY' -or $_.ResourcesAfterExit -ne 0 -or
        $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or $_.StateUnchanged -ne $true -or
        $_.RestoreExecuted -ne $false -or $_.NativeConfirmation -ne $false -or $_.ActualPowerLoss -ne $false }).Count){
    throw 'Three same-package read-only recovery window gates are incomplete or invalid.'
}
$taskData=Join-Path $taskRepo ('out\manual\v3-04-l-r4\'+$Scenario)
if($CheckOnly){[pscustomobject]@{Result='CANDIDATE CHECKED - MANUAL PENDING';Scenario=$Scenario;DataRoot=$taskData;
    Artifacts=$taskChecked;AutomatedGates=35;ContentGates=14;RelatedRegressionGates=21;ArtifactFingerprint=$taskFingerprint};return}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit existing DesktopUp normally first. No process was stopped.'}
if(!(Test-Path -LiteralPath $taskData)){
    & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData --content-layout $Scenario | Out-Null
    if($LASTEXITCODE -ne 0){throw 'New isolated content fixture failed. Partial data was preserved.'}
}elseif(!(Test-Path -LiteralPath $taskData -PathType Container) -or !(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)){
    throw "Existing profile is incomplete; inspect it without resetting: $taskData"
}
$taskArguments='--show-control-center --animation-config="'+(Join-Path $taskPackage 'app\config\poster-animation.json')+'"'
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - CONTENT LAYOUT MANUAL PENDING';Scenario=$Scenario;ProcessId=$taskProcess.Id;DataRoot=$taskData;
    LogPath=(Join-Path $taskData 'logs\desktopup.jsonl');ArtifactFingerprint=$taskFingerprint;
    Scope='DEMO only; existing manual edits preserved; native mouse/keyboard/wheel and visual behavior require human verification'}
