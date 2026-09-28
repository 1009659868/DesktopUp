#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
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
function Get-ContentBrowseBinding {
    foreach($taskArea in @('app','fixture')) {
        $taskFolder=if($taskArea -eq 'app'){$taskApp}else{$taskFixture}
        foreach($taskFile in (Get-ChildItem -LiteralPath $taskFolder -Recurse -File | Sort-Object FullName)) {
            [pscustomobject]@{Path=$taskArea+'/'+$taskFile.FullName.Substring($taskFolder.Length).TrimStart('\').Replace('\','/');
                Length=$taskFile.Length;SHA256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash}
        }
    }
    foreach($taskName in @('probe-content-browse.ps1','Start-DesktopUpFixtureProcess.ps1')) {
        $taskFile=Get-Item -LiteralPath (Join-Path $PSScriptRoot $taskName)
        [pscustomobject]@{Path='scripts/'+$taskName;Length=$taskFile.Length;
            SHA256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash}
    }
}
$taskBinding=@(Get-ContentBrowseBinding)
if($CheckOnly) {
    [pscustomobject]@{Result='READY - NOT EXECUTED';OutputRoot=$taskOutput;Artifacts=$taskBinding.Count;ExpectedAssemblySha256=$ExpectedAssemblySha256}
    return
}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit existing DesktopUp normally first; no process was stopped.'}
New-Item -ItemType Directory -Path (Join-Path $taskOutput 'scripts') | Out-Null
foreach($taskName in @('probe-content-browse.ps1','Start-DesktopUpFixtureProcess.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskName) -Destination (Join-Path $taskOutput ('scripts/'+$taskName))
}
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData=Join-Path $taskOutput 'data'
$taskFixtureOutput=& $taskFixtureExe $taskData --content-layout hundred 2>&1
$taskFixtureExit=$LASTEXITCODE
$taskFixtureOutput | Set-Content -LiteralPath (Join-Path $taskOutput 'fixture-create.log') -Encoding UTF8
if($taskFixtureExit -ne 0){throw 'Hundred-item isolated fixture creation failed; partial evidence preserved.'}
$taskSeedHash=(Get-FileHash -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -Algorithm SHA256).Hash
[pscustomobject]@{Scenario='hundred';DataRoot=$taskData;DatabaseSha256=$taskSeedHash;
    FixtureAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $taskFixture 'DesktopUp.SessionFixture.dll') -Algorithm SHA256).Hash} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'fixture-seed.json') -Encoding UTF8
$taskArguments='--content-browse-probe --exit-after-ms=85000 --animation-config="'+(Join-Path $taskApp 'config\poster-animation.json')+'"'
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskDeadline=[DateTime]::UtcNow.AddSeconds(90)
while(!$taskProcess.WaitForExit(500)) {
    if([DateTime]::UtcNow -gt $taskDeadline){throw "Content browsing probe timed out; process preserved for normal exit: $($taskProcess.Id)"}
}
$taskLog=Join-Path $taskData 'logs\desktopup.jsonl'
if(!(Test-Path -LiteralPath $taskLog)){throw 'No application log produced; evidence preserved.'}
$taskEvents=@(Get-Content -LiteralPath $taskLog -Encoding UTF8 | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $taskProcess.Id)
$taskDone=@($taskEvents | Where-Object eventName -eq 'content.browse_observed')
$taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
$taskErrors=@($taskEvents | Where-Object level -in @('error','critical','fatal'))
$taskStable=($taskBinding | ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-ContentBrowseBinding) | ConvertTo-Json -Depth 5 -Compress)
$taskPngs=@(Get-ChildItem -LiteralPath $taskData -Filter 'content-browse-*.png' -File | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{Name=$_.Name;Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$taskExpectedCaptures=@('content-browse-horizontal-head-after-up.png','content-browse-horizontal-head-before.png',
    'content-browse-vertical-head-after-up.png','content-browse-vertical-head-before.png')
$taskExpectedCases=@('vertical-loop-seam','vertical-bidirectional','vertical-small-delta','vertical-finite','vertical-disable-while-wrapped',
    'horizontal-loop-seam','horizontal-bidirectional','horizontal-small-delta','horizontal-finite','horizontal-disable-while-wrapped',
    'vertical-short-content','horizontal-short-content','vertical-loop-drag','horizontal-loop-drag',
    'vertical-frame-minimum','horizontal-frame-minimum')
$taskObservation=if($taskDone.Count -eq 1){$taskDone[0].data}else{$null}
$taskObservedCases=@($taskObservation.observation.cases)
$taskCasesComplete=$taskObservedCases.Count -eq 16 -and
    (@($taskObservedCases | Sort-Object) -join '|') -ceq (@($taskExpectedCases | Sort-Object) -join '|')
$taskCaptureComplete=(@($taskPngs.Name) -join '|') -ceq ($taskExpectedCaptures -join '|')
$taskScopeCorrect=$null -ne $taskObservation -and !$taskObservation.nativeMouse -and !$taskObservation.nativeKeyboard -and
    !$taskObservation.nativeWheel -and !$taskObservation.desktopPixelVisualCertified -and !$taskObservation.captureIncludesSystemBackdrop
$taskPassed=$taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskObservation.passed -and
    $taskObservation.observation.passed -and $taskObservation.observation.wheelEvents -eq 102 -and $taskCasesComplete -and
    $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0 -and
    $taskStable -and $taskCaptureComplete -and @($taskPngs | Where-Object Length -le 0).Count -eq 0 -and $taskScopeCorrect
$taskResult=[pscustomobject]@{Passed=$taskPassed;Result=$(if($taskPassed){'OBSERVED - MANAGED SCROLL/LOOP-DRAG/XAML/STATE ONLY'}else{'FAILED - EVIDENCE PRESERVED'});
    ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;DataRoot=$taskData;ExpectedAssemblySha256=$ExpectedAssemblySha256;
    ArtifactsStable=$taskStable;FixtureScenario='hundred';FixtureSeedDatabaseSha256=$taskSeedHash;
    CasesComplete=$taskCasesComplete;CapturesComplete=$taskCaptureComplete;Pngs=$taskPngs;
    Observation=$taskObservation;Errors=$taskErrors;Cleanup=@($taskStopped | ForEach-Object {$_.data});
    NativeMouse=$false;NativeKeyboard=$false;NativeWheel=$false;ProcessRestartTested=$false;
    DesktopPixelVisualCertified=$false;CaptureIncludesSystemBackdrop=$false;UiCProductAccepted=$false;MilestoneAccepted=$false}
$taskResult | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if(!$taskPassed){throw "Content browsing observation failed; inspect $(Join-Path $taskOutput 'observation.json')"}
$taskResult | Select-Object Passed,Result,ProcessId,ArtifactsStable,CasesComplete,CapturesComplete
