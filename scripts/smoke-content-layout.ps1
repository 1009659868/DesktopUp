#requires -Version 5.1
param([string]$AppDirectory,[string]$FixtureDirectory,[string]$OutputRoot,
    [ValidateSet('three','empty','hundred','thousand','resource-one','resource-three','resource-six')]
    [string[]]$Scenarios=@('three','empty','hundred','thousand','resource-one','resource-three','resource-six'),[switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskPackage=Join-Path $taskRepo 'out\verify\v3-04-l-r4'
if(!$AppDirectory){$AppDirectory=Join-Path $taskPackage 'app'}
if(!$FixtureDirectory){$FixtureDirectory=Join-Path $taskPackage 'fixture'}
if(!$OutputRoot){$OutputRoot=$taskPackage}
$taskApp=[IO.Path]::GetFullPath($AppDirectory)
$taskFixture=[IO.Path]::GetFullPath($FixtureDirectory)
$taskOutput=[IO.Path]::GetFullPath($OutputRoot)
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if($taskOutput.Equals($taskNormal,[StringComparison]::OrdinalIgnoreCase) -or $taskOutput.StartsWith($taskNormal+'\',[StringComparison]::OrdinalIgnoreCase)){
    throw 'Content gates cannot use the normal DesktopUp profile.'
}
$taskFiles=@{
    app=@('WinUIEx.dll','DesktopUp.Application.deps.json','DesktopUp.Application.dll','DesktopUp.deps.json','DesktopUp.dll',
        'DesktopUp.Domain.deps.json','DesktopUp.Domain.dll','DesktopUp.exe','DesktopUp.Infrastructure.deps.json','DesktopUp.Infrastructure.dll',
        'DesktopUp.pri','DesktopUp.runtimeconfig.json','DesktopUp.Windows.deps.json','DesktopUp.Windows.dll','config/poster-animation.json')
    fixture=@('DesktopUp.Application.deps.json','DesktopUp.Application.dll','DesktopUp.Domain.deps.json','DesktopUp.Domain.dll',
        'DesktopUp.Infrastructure.deps.json','DesktopUp.Infrastructure.dll','DesktopUp.SessionFixture.deps.json','DesktopUp.SessionFixture.dll',
        'DesktopUp.SessionFixture.exe','DesktopUp.SessionFixture.runtimeconfig.json')
}
function Get-ContentCandidateBinding {
    $taskRows=@();$taskMap=@{}
    foreach($taskArea in @('app','fixture')){
        $taskMap[$taskArea]=@{}
        $taskDirectory=if($taskArea -eq 'app'){$taskApp}else{$taskFixture}
        foreach($taskName in ($taskFiles[$taskArea] | Sort-Object)){
            $taskPath=Join-Path $taskDirectory $taskName
            if(!(Test-Path -LiteralPath $taskPath -PathType Leaf)){throw "Missing candidate artifact: $taskPath"}
            $taskHash=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToUpperInvariant()
            $taskMap[$taskArea][$taskName]=$taskHash
            $taskRows+=($taskArea+'/'+$taskName+'='+$taskHash)
        }
    }
    $taskSha=[Security.Cryptography.SHA256]::Create()
    try{$taskDigest=([BitConverter]::ToString($taskSha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($taskRows -join "`n"))))).Replace('-','')}
    finally{$taskSha.Dispose()}
    [pscustomobject]@{Fingerprint=$taskDigest;Artifacts=$taskMap}
}
$taskBinding=Get-ContentCandidateBinding
if($CheckOnly){[pscustomobject]@{Result='READY - NOT EXECUTED';Package=$taskPackage;Scenarios=$Scenarios;ArtifactFingerprint=$taskBinding.Fingerprint};return}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit existing DesktopUp normally first. No process was stopped.'}
if(!$Scenarios.Count){throw 'At least one isolated scenario is required.'}
$taskEvidence=Join-Path $taskOutput ('content-gates-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $taskEvidence
$taskExecutable=Join-Path $taskApp 'DesktopUp.exe'
$taskFixtureExecutable=Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
function Invoke-ContentGate {
    param([string]$Scenario,[string]$Data,[switch]$Restart)
    $taskFlag=if($Restart){'--content-layout-restart'}else{'--content-layout-smoke'}
    $taskArguments=$taskFlag+' --content-layout-case='+$Scenario+' --exit-after-ms=90000 --animation-config="'+(Join-Path $taskApp 'config\poster-animation.json')+'"'
    $taskLog=Join-Path $Data 'logs\desktopup.jsonl'
    $taskPrevious=if(Test-Path -LiteralPath $taskLog){[IO.File]::ReadAllLines($taskLog).Count}else{0}
    $taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskExecutable -DataRoot $Data -Arguments $taskArguments -WindowStyle Hidden
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(90)
    while(!$taskProcess.WaitForExit(1000)){
        if([DateTime]::UtcNow -gt $taskDeadline){throw "Content gate timed out; process preserved for normal exit: $($taskProcess.Id)"}
    }
    if(!(Test-Path -LiteralPath $taskLog)){throw "Missing content process log: $Data"}
    $taskEvents=@(Get-Content -LiteralPath $taskLog | Select-Object -Skip $taskPrevious | ForEach-Object{$_ | ConvertFrom-Json})
    $taskExpected=if($Restart){'content_layout.restart_passed'}else{'content_layout.smoke_passed'}
    $taskDone=@($taskEvents | Where-Object eventName -eq $taskExpected)
    $taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
    $taskErrors=@($taskEvents | Where-Object{$_.level -in @('error','critical','fatal') -or $_.data.passed -eq $false})
    if($taskProcess.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or !$taskDone[0].data.passed -or
        $taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0 -or $taskErrors.Count){
        $taskFailure=($taskErrors | ConvertTo-Json -Depth 10 -Compress)
        throw "Content gate failed: $Scenario restart=$Restart; $Data; $taskFailure"
    }
    $taskProof=$taskDone[0].data
    $taskCount=if($Scenario -in @('three','resource-three')){3}elseif($Scenario -eq 'resource-six'){6}else{1}
    if($taskProof.scenario -ne $Scenario -or [bool]$taskProof.restart -ne [bool]$Restart -or $taskProof.posters -ne $taskCount -or
        $taskProof.nativeMouse -ne $false -or $taskProof.nativeKeyboard -ne $false -or $taskProof.nativeWheel -ne $false -or
        $taskProof.resourceSamples -ne 12 -or $taskProof.sampledSeconds -lt 10 -or $taskProof.checks -lt 10){throw 'Completion evidence has the wrong scenario/scope/idle sample duration.'}
    $taskRequired=if($Restart){@('same-root-restart-preserves-content-identities-and-settings')}elseif($Scenario -eq 'three'){
        @('reference-illustration','reference-covers','reference-landscape','guides-False-False','guides-True-False','guides-True-True','guides-False-True',
            'resize-single-revision-target-only','move-single-revision-target-only','undo-after-commit','redo-after-commit','order-pin-single-revision',
            'manual-exit-restores-automatic','legacy-hard-position-conversion-rejected','legacy-region-membership-replacement-rejected',
            'complete-aggregate-cas-stale-rejection','r4-collapse-prepare-and-restoration','r4-expand-prepare-and-restoration',
            'legacy-regions-fit-cross-axis-without-second-scrollbar','heat-two-proposals-protected-latest-pending',
            'heat-safe-point-latest-only-override-preserved-no-persistence','heat-old-itemset-rejected-after-membership-replacement',
            'content-input-latest-release','content-input-reverse-origin','content-input-cancel-draft','content-input-edge-release',
            'theme-metrics-protected-latest-pending','theme-safe-point-latest-last-good-no-native-surface',
            'queued-proposal-suspended-input-remains-invalid','shared-recovery-resumes-proposal-on-safe-point')
    }elseif($Scenario -in @('hundred','thousand')){@('partial-card-clip-and-hit-agree','scroll-bottom-reaches-last-item-with-bounded-controls')}
    elseif($Scenario -eq 'empty'){@('zero-items-no-fabricated-cards-or-hitboxes')}else{@('actual-canvas-snapshot-input-and-bounded-controls','comparable-six-equal-basic-box-300x360-workload')}
    if(@($taskRequired | Where-Object{$_ -notin $taskProof.cases}).Count){throw 'Required content production cases did not run.'}
    if((Get-ContentCandidateBinding).Fingerprint -ne $taskBinding.Fingerprint){throw 'Candidate changed during validation; results do not bind one build.'}
    [pscustomobject]@{Result='PASS - PRODUCTION COMMAND/CANVAS ONLY';Scenario=$Scenario;Restart=[bool]$Restart;Posters=$taskCount;
        ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;Checks=$taskProof.checks;Cases=@($taskProof.cases);
        ResourcesAfterExit=$taskStopped[0].data.activeResources;ResourceSamples=$taskProof.resourceSamples;ResourceReadings=$taskProof.resources;
        SampledSeconds=$taskProof.sampledSeconds;ProcessCpuPercent=$taskProof.processCpuPercent;
        DataRoot=$Data;NativeMouse=$false;NativeKeyboard=$false;NativeWheel=$false;PixelVisualCertified=$false;
        ArtifactFingerprint=$taskBinding.Fingerprint;AppHash=$taskBinding.Artifacts.app.'DesktopUp.dll';
        FixtureHash=$taskBinding.Artifacts.fixture.'DesktopUp.SessionFixture.dll'}
}
$taskResults=@()
foreach($taskScenario in ($Scenarios | Sort-Object -Unique)){
    $taskData=Join-Path $taskEvidence $taskScenario
    & $taskFixtureExecutable $taskData --content-layout $taskScenario | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Content fixture failed; partial data preserved.'}
    foreach($taskRestart in @($false,$true)){
        $taskResult=Invoke-ContentGate -Scenario $taskScenario -Data $taskData -Restart:$taskRestart
        $taskResults+=$taskResult
        $taskResult | Select-Object Result,Scenario,Restart,Posters,ProcessId,Checks,ResourcesAfterExit,DataRoot
    }
}
$taskResults | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskEvidence 'results.json') -Encoding UTF8
$taskAll=@('empty','hundred','resource-one','resource-six','resource-three','thousand','three')
if($taskResults.Count -eq 14 -and !(Compare-Object $taskAll ($Scenarios | Sort-Object -Unique))){
    $taskResults | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskOutput 'content-layout-gates-passed.json') -Encoding UTF8
}
