#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [ValidateSet(1,3,6)][int]$PosterCount=3,
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp=(Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskFixture=(Resolve-Path -LiteralPath $FixtureDirectory).Path.TrimEnd('\')
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach($taskProtected in @($taskApp,$taskFixture,$taskNormal)) {
    if($taskOutput.Equals($taskProtected,[StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Use an output directory isolated from artifacts and the ordinary profile.'}
}
if(Test-Path -LiteralPath $taskOutput){throw 'Preserve existing evidence; choose a new OutputRoot.'}
$taskExe=Join-Path $taskApp 'DesktopUp.exe'
$taskDll=Join-Path $taskApp 'DesktopUp.dll'
$taskFixtureExe=Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
foreach($taskPath in @($taskExe,$taskDll,$taskFixtureExe)) {
    if(!(Test-Path -LiteralPath $taskPath -PathType Leaf)){throw "Missing artifact: $taskPath"}
}
if((Get-FileHash -LiteralPath $taskDll -Algorithm SHA256).Hash -ine $ExpectedAssemblySha256){throw 'Candidate DLL hash differs.'}
function Get-ContentSourceBinding {
    foreach($taskArea in @('app','fixture')) {
        $taskFolder=if($taskArea -eq 'app'){$taskApp}else{$taskFixture}
        foreach($taskFile in (Get-ChildItem -LiteralPath $taskFolder -Recurse -File | Sort-Object FullName)) {
            [pscustomobject]@{Path=$taskArea+'/'+$taskFile.FullName.Substring($taskFolder.Length).TrimStart('\').Replace('\','/');
                Length=$taskFile.Length;SHA256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash}
        }
    }
    foreach($taskName in @('probe-content-source.ps1','Start-DesktopUpFixtureProcess.ps1')) {
        $taskFile=Get-Item -LiteralPath (Join-Path $PSScriptRoot $taskName)
        [pscustomobject]@{Path='scripts/'+$taskName;Length=$taskFile.Length;
            SHA256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash}
    }
}
$taskBinding=@(Get-ContentSourceBinding)
if($CheckOnly) {
    [pscustomobject]@{Result='READY - NOT EXECUTED';OutputRoot=$taskOutput;Artifacts=$taskBinding.Count;PosterCount=$PosterCount}
    return
}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit existing DesktopUp normally first; no process was stopped.'}
New-Item -ItemType Directory -Path (Join-Path $taskOutput 'scripts') | Out-Null
foreach($taskName in @('probe-content-source.ps1','Start-DesktopUpFixtureProcess.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskName) -Destination (Join-Path $taskOutput ('scripts/'+$taskName))
}
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData=Join-Path $taskOutput 'data'
$taskScenario=@{1='resource-one';3='resource-three';6='resource-six'}[$PosterCount]
$taskFixtureOutput=& $taskFixtureExe $taskData --content-layout $taskScenario 2>&1
$taskFixtureExit=$LASTEXITCODE
$taskFixtureOutput | Set-Content -LiteralPath (Join-Path $taskOutput 'fixture-create.log') -Encoding UTF8
if($taskFixtureExit -ne 0){throw 'Resource fixture creation failed; partial evidence preserved.'}
$taskSeedHash=(Get-FileHash -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -Algorithm SHA256).Hash
[pscustomobject]@{Scenario=$taskScenario;DataRoot=$taskData;DatabaseSha256=$taskSeedHash} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'fixture-seed.json') -Encoding UTF8
$taskArguments='--content-source-probe --exit-after-ms=70000 --animation-config="'+(Join-Path $taskApp 'config\poster-animation.json')+'"'
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskDeadline=[DateTime]::UtcNow.AddSeconds(80)
while(!$taskProcess.WaitForExit(500)) {
    if([DateTime]::UtcNow -gt $taskDeadline){throw "Content source probe timed out; process preserved for normal exit: $($taskProcess.Id)"}
}
$taskLog=Join-Path $taskData 'logs\desktopup.jsonl'
if(!(Test-Path -LiteralPath $taskLog)){throw 'No application log produced; evidence preserved.'}
$taskEvents=@(Get-Content -LiteralPath $taskLog -Encoding UTF8 | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $taskProcess.Id)
$taskDone=@($taskEvents | Where-Object eventName -eq 'content.source_observed')
$taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
$taskErrors=@($taskEvents | Where-Object level -in @('error','critical','fatal'))
$taskStable=($taskBinding | ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-ContentSourceBinding) | ConvertTo-Json -Depth 5 -Compress)
$taskObservation=if($taskDone.Count -eq 1){$taskDone[0].data}else{$null}
$taskExpected=@('bounded-source-churn-and-slot-replenishment','availability-recovery-gesture-cancel-and-open-dedup',
    'membership-cas-late-release-and-poster-isolation','hidden-late-result-and-session-rebind','source-notification-r4-restore-safe-point')
$taskCasesComplete=(@($taskObservation.observation.cases | Sort-Object) -join '|') -ceq (@($taskExpected | Sort-Object) -join '|')
$taskCapture=Join-Path $taskData 'content-source-ready.png'
$taskCaptureReady=(Test-Path -LiteralPath $taskCapture) -and (Get-Item -LiteralPath $taskCapture).Length -gt 0
$taskScopeCorrect=$null -ne $taskObservation -and !$taskObservation.nativeInput -and !$taskObservation.realFiles -and
    !$taskObservation.observation.bitmapDecode -and !$taskObservation.uiCProductAccepted
$taskPassed=$taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskObservation.passed -and
    $taskObservation.observation.passed -and $taskObservation.observation.posters -eq $PosterCount -and $taskCasesComplete -and
    $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0 -and
    $taskStable -and $taskCaptureReady -and $taskScopeCorrect
$taskResult=[pscustomobject]@{Passed=$taskPassed;Result=$(if($taskPassed){'PASS - MANAGED FAKE SOURCE / REQUEST LIFETIME ONLY'}else{'FAILED - EVIDENCE PRESERVED'});
    ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;DataRoot=$taskData;ExpectedAssemblySha256=$ExpectedAssemblySha256;
    ArtifactsStable=$taskStable;FixtureScenario=$taskScenario;FixtureSeedDatabaseSha256=$taskSeedHash;
    CasesComplete=$taskCasesComplete;CaptureReady=$taskCaptureReady;
    Observation=$taskObservation;Errors=$taskErrors;Cleanup=@($taskStopped | ForEach-Object {$_.data});
    NativeInput=$false;RealFiles=$false;BitmapDecode=$false;MilestoneAccepted=$false}
$taskResult | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if(!$taskPassed){throw "Content source observation failed; inspect $(Join-Path $taskOutput 'observation.json')"}
$taskResult | Select-Object Passed,Result,ProcessId,FixtureScenario,ArtifactsStable,CasesComplete,CaptureReady
