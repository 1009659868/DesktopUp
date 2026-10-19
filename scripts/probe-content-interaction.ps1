#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [ValidateSet('Daily','Protocol','RejectedHeat','NativeMouse','Native')][string]$Mode='Protocol'
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp=(Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskFixture=(Resolve-Path -LiteralPath $FixtureDirectory).Path.TrimEnd('\')
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach($taskProtected in @($taskApp,$taskFixture,$taskNormal)) {
    if($taskOutput.Equals($taskProtected,[StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Output must be isolated from application and ordinary data.'}
}
if(Test-Path -LiteralPath $taskOutput){throw 'Preserve existing evidence and use a fresh OutputRoot.'}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'An existing DesktopUp process must exit normally first; no process stopped.'}
if('DesktopUpContentNativeDriver' -as [type]){throw 'Use a fresh PowerShell process so the driver source and loaded type cannot differ.'}
$taskExe=Join-Path $taskApp 'DesktopUp.exe'
$taskDll=Join-Path $taskApp 'DesktopUp.dll'
$taskFixtureExe=Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
foreach($taskPath in @($taskExe,$taskDll,$taskFixtureExe)){if(!(Test-Path -LiteralPath $taskPath -PathType Leaf)){throw "Missing artifact $taskPath"}}
if((Get-FileHash -LiteralPath $taskDll -Algorithm SHA256).Hash -ine $ExpectedAssemblySha256){throw 'Candidate DLL hash differs.'}
function Get-ContentCandidateBinding {
    foreach($taskArea in @('app','fixture')) {
        $taskFolder=if($taskArea -eq 'app'){$taskApp}else{$taskFixture}
        Get-ChildItem -LiteralPath $taskFolder -File -Recurse | Sort-Object FullName | ForEach-Object {
            [pscustomobject]@{Path=$taskArea+'/'+$_.FullName.Substring($taskFolder.Length).TrimStart('\').Replace('\','/');
                Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
        }
    }
    foreach($taskName in @('probe-content-interaction.ps1','Invoke-DesktopUpContentNativeInput.ps1','DesktopUpContentNativeDriver.cs','Start-DesktopUpFixtureProcess.ps1')) {
        $taskFile=Get-Item -LiteralPath (Join-Path $PSScriptRoot $taskName)
        [pscustomobject]@{Path='scripts/'+$taskName;Length=$taskFile.Length;SHA256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash}
    }
}
$taskBinding=@(Get-ContentCandidateBinding)
New-Item -ItemType Directory -Path $taskOutput | Out-Null
New-Item -ItemType Directory -Path (Join-Path $taskOutput 'scripts') | Out-Null
foreach($taskName in @('probe-content-interaction.ps1','Invoke-DesktopUpContentNativeInput.ps1','DesktopUpContentNativeDriver.cs','Start-DesktopUpFixtureProcess.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskName) -Destination (Join-Path $taskOutput ('scripts/'+$taskName))
}
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData=Join-Path $taskOutput 'data'
if($Mode -eq 'RejectedHeat'){& $taskFixtureExe $taskData --content-layout rejected-heat}
else{& $taskFixtureExe $taskData 2}
if($LASTEXITCODE -ne 0){throw 'Fixture creation failed; partial evidence retained.'}
$taskRuns=@()
function Invoke-ContentProbeProcess([string]$taskFlag,[bool]$taskDrive) {
    $taskArguments=$taskFlag+' --exit-after-ms=95000 --animation-config="'+(Join-Path $taskApp 'config\poster-animation.json')+'"'
    $taskFeedbackDiagnostics=$Mode -eq 'Protocol'
    $taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden -ContentFeedbackDiagnostics:$taskFeedbackDiagnostics
    $taskSeen=New-Object 'System.Collections.Generic.HashSet[string]'
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(100)
    while(!$taskProcess.WaitForExit(100)) {
        if([DateTime]::UtcNow -gt $taskDeadline){throw "Probe exceeded deadline; process preserved: $($taskProcess.Id)"}
        if(!$taskDrive){continue}
        foreach($taskRequestFile in (Get-ChildItem -LiteralPath $taskData -Filter 'native-request-*.json' -File)) {
            if($taskSeen.Contains($taskRequestFile.Name)){continue}
            # The App writes one short file synchronously. Retry an incomplete read before any input.
            try{$taskRequest=Get-Content -LiteralPath $taskRequestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json}catch{continue}
            if($taskRequest.processId -ne $taskProcess.Id -or $taskRequest.requestId -notmatch '^0[0-7]$'){throw 'Native request has invalid process or sequence identity.'}
            $taskSeen.Add($taskRequestFile.Name) | Out-Null
            $taskParams=@{ProcessId=$taskProcess.Id;InputWindowHandle=[string]$taskRequest.InputWindowHandle;
                ExpectedExecutablePath=$taskExe;Action=[string]$taskRequest.action;StartX=[int]$taskRequest.startX;StartY=[int]$taskRequest.startY}
            if($taskRequest.action -in @('drag','cancel-drag')){$taskParams.EndX=[int]$taskRequest.endX;$taskParams.EndY=[int]$taskRequest.endY}
            $taskResult=& (Join-Path $PSScriptRoot 'Invoke-DesktopUpContentNativeInput.ps1') @taskParams
            $taskResponse=Join-Path $taskData ('native-response-'+$taskRequest.requestId+'.json')
            if(Test-Path -LiteralPath $taskResponse){throw 'Response evidence already exists.'}
            $taskTemp=$taskResponse+'.tmp'
            $taskResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskTemp -Encoding UTF8
            Move-Item -LiteralPath $taskTemp -Destination $taskResponse
        }
    }
    $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') -Encoding UTF8 | ForEach-Object {$_|ConvertFrom-Json} | Where-Object processId -eq $taskProcess.Id)
    $taskDone=@($taskEvents|Where-Object eventName -eq 'content.interaction_observed')
    $taskStopped=@($taskEvents|Where-Object eventName -eq 'application.stopped')
    $taskErrors=@($taskEvents|Where-Object {$_.level -in @('error','critical','fatal')})
    $taskPassed=$taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskDone[0].data.passed -and
        $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0
    if($taskFlag -eq '--content-protocol-probe') {
        $taskProtocol=@($taskDone | ForEach-Object { $_.data.observations })
        $taskPassed=$taskPassed -and $taskProtocol.Count -eq 1 -and
            $taskProtocol[0].FeedbackDiagnosticsEnabled -ceq $taskFeedbackDiagnostics
    }
    [pscustomobject]@{Flag=$taskFlag;ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;Passed=$taskPassed;ContentFeedbackDiagnostics=$taskFeedbackDiagnostics;
        Observation=@($taskDone|ForEach-Object {$_.data});Cleanup=@($taskStopped|ForEach-Object {$_.data});Errors=$taskErrors}
}
$taskFlag= switch ($Mode) { 'Native' {'--content-native-probe'} 'NativeMouse' {'--content-native-mouse-probe'} 'RejectedHeat' {'--content-rejected-heat-probe'} default {'--content-protocol-probe'} }
$taskFirst=Invoke-ContentProbeProcess $taskFlag ($Mode -in @('Native','NativeMouse'))
$taskRuns+= $taskFirst
if($taskFirst.Passed){$taskRuns+=Invoke-ContentProbeProcess '--content-interaction-restart' $false}
$taskStable=($taskBinding|ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-ContentCandidateBinding)|ConvertTo-Json -Depth 5 -Compress)
$taskPassed=$taskStable -and $taskRuns.Count -eq 2 -and @($taskRuns|Where-Object {!$_.Passed}).Count -eq 0
$taskResult=[pscustomobject]@{Passed=$taskPassed;Mode=$Mode;ContentFeedbackDiagnostics=($Mode -eq 'Protocol');ExpectedAssemblySha256=$ExpectedAssemblySha256;
    ArtifactsStable=$taskStable;Runs=$taskRuns;UiCProductAccepted=$false;MilestoneAccepted=$false}
$taskResult|ConvertTo-Json -Depth 50|Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if(!$taskPassed){throw "Content interaction gate did not pass; evidence: $taskOutput"}
$taskResult|Select-Object Passed,Mode,ArtifactsStable,UiCProductAccepted
