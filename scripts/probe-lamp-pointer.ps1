#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [ValidateRange(0,10000)][int]$WaitForUnoccludedMilliseconds=0
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp=(Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskFixture=(Resolve-Path -LiteralPath $FixtureDirectory).Path.TrimEnd('\')
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach($taskProtected in @($taskApp,$taskFixture,$taskNormal)) {
    if($taskOutput.Equals($taskProtected,[StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be isolated from application and ordinary data.'}
}
if(Test-Path -LiteralPath $taskOutput){throw 'Preserve existing evidence and use a fresh OutputRoot.'}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
$taskExe=Join-Path $taskApp 'DesktopUp.exe'
if((Get-FileHash -LiteralPath (Join-Path $taskApp 'DesktopUp.dll') -Algorithm SHA256).Hash -ine $ExpectedAssemblySha256){throw 'Candidate DLL hash differs.'}
function Get-PointerBinding {
    foreach($taskArea in @('app','fixture')) {
        $taskFolder=if($taskArea -eq 'app'){$taskApp}else{$taskFixture}
        Get-ChildItem -LiteralPath $taskFolder -File -Recurse|Sort-Object FullName|ForEach-Object {
            [pscustomobject]@{Path=$taskArea+'/'+$_.FullName.Substring($taskFolder.Length).TrimStart('\').Replace('\','/');Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
        }
    }
    foreach($taskName in @('probe-lamp-pointer.ps1','Invoke-DesktopUpContentNativeInput.ps1','DesktopUpContentNativeDriver.cs','Start-DesktopUpFixtureProcess.ps1')) {
        $taskFile=Get-Item -LiteralPath (Join-Path $PSScriptRoot $taskName)
        [pscustomobject]@{Path='scripts/'+$taskName;Length=$taskFile.Length;SHA256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash}
    }
}
$taskBinding=@(Get-PointerBinding)
New-Item -ItemType Directory -Path $taskOutput|Out-Null
$taskBinding|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskData=Join-Path $taskOutput 'data'
& (Join-Path $taskFixture 'DesktopUp.SessionFixture.exe') $taskData 2
if($LASTEXITCODE -ne 0){throw 'Fixture creation failed.'}
$taskArguments='--lamp-pointer-probe --exit-after-ms=100000 --animation-config="'+(Join-Path $taskApp 'config\poster-animation.json')+'"'
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskSeen=New-Object 'System.Collections.Generic.HashSet[string]'
$taskDrivers=@()
$taskPreflightAttempts=@()
$taskDeadline=[DateTime]::UtcNow.AddSeconds(110)
while(!$taskProcess.WaitForExit(100)) {
    if([DateTime]::UtcNow -gt $taskDeadline){throw "Probe deadline exceeded; process preserved for normal exit: $($taskProcess.Id)"}
    $taskRequestFile=Join-Path $taskData 'lamp-pointer-request.json'
    if(!(Test-Path -LiteralPath $taskRequestFile)){continue}
    try{$taskRequest=Get-Content -LiteralPath $taskRequestFile -Raw -Encoding UTF8|ConvertFrom-Json}catch{continue}
    if($taskSeen.Contains([string]$taskRequest.RequestId)){continue}
    if($taskRequest.ProcessId -ne $taskProcess.Id -or $taskRequest.Stage -notin @('inside-1','inside-2','outside-peer')){throw 'Invalid pointer request identity or stage.'}
    $taskSeen.Add([string]$taskRequest.RequestId)|Out-Null
    $taskVisibleDeadline=[DateTime]::UtcNow.AddMilliseconds($WaitForUnoccludedMilliseconds)
    do {
        $taskDriver=& (Join-Path $PSScriptRoot 'Invoke-DesktopUpContentNativeInput.ps1') -ProcessId $taskProcess.Id -InputWindowHandle ([string]$taskRequest.InputHandle) -ExpectedExecutablePath $taskExe -Action move -StartX ([int]$taskRequest.Point.X) -StartY ([int]$taskRequest.Point.Y)
        # Only a zero-input occlusion may wait. Never replay a move or any partial action.
        $taskCanWait=$taskDriver.Status -eq 'Occluded' -and !$taskDriver.Executed -and !$taskDriver.CompletedAction -and
            $taskDriver.MouseMoves -eq 0 -and $taskDriver.LeftDowns -eq 0 -and $taskDriver.RightDowns -eq 0 -and
            $taskDriver.CleanupSucceeded -and [DateTime]::UtcNow -lt $taskVisibleDeadline -and !$taskProcess.HasExited
        if(!$taskCanWait){break}
        $taskPreflightAttempts+=[pscustomobject]@{RequestId=$taskRequest.RequestId;Stage=$taskRequest.Stage;Result=$taskDriver}
        Start-Sleep -Milliseconds 500
    } while($true)
    $taskDrivers+= [pscustomobject]@{Request=$taskRequest;Result=$taskDriver}
    $taskResponse=[pscustomobject]@{RequestId=$taskRequest.RequestId;Status=$(if($taskDriver.CompletedAction){'moved'}else{'blocked'});Reason=$taskDriver.FailureReason;DriverResult=$taskDriver}
    $taskResponseFile=Join-Path $taskData 'lamp-pointer-response.json'
    $taskResponse|ConvertTo-Json -Depth 10|Set-Content -LiteralPath ($taskResponseFile+'.tmp') -Encoding UTF8
    Move-Item -LiteralPath ($taskResponseFile+'.tmp') -Destination $taskResponseFile -Force
}
$taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') -Encoding UTF8|ForEach-Object {$_|ConvertFrom-Json}|Where-Object processId -eq $taskProcess.Id)
$taskDone=@($taskEvents|Where-Object eventName -eq 'lamp.pointer_observed')
$taskStopped=@($taskEvents|Where-Object eventName -eq 'application.stopped')
$taskErrors=@($taskEvents|Where-Object level -in @('error','critical','fatal'))
$taskStable=($taskBinding|ConvertTo-Json -Depth 5 -Compress) -ceq (@(Get-PointerBinding)|ConvertTo-Json -Depth 5 -Compress)
$taskPassed=$taskProcess.ExitCode -eq 0 -and $taskDone.Count -eq 1 -and $taskDone[0].data.passed -and $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0 -and $taskStable -and $taskDrivers.Count -eq 3 -and @($taskDrivers|Where-Object {!$_.Result.CompletedAction -or $_.Result.LeftDowns -ne 0 -or $_.Result.RightDowns -ne 0}).Count -eq 0
$taskResult=[pscustomobject]@{Passed=$taskPassed;ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;ArtifactsStable=$taskStable;ExpectedAssemblySha256=$ExpectedAssemblySha256;Observation=@($taskDone|ForEach-Object {$_.data});Drivers=$taskDrivers;PreflightAttempts=$taskPreflightAttempts;WaitForUnoccludedMilliseconds=$WaitForUnoccludedMilliseconds;Cleanup=@($taskStopped|ForEach-Object {$_.data});Errors=$taskErrors;NativePointer=$true;NativeButtons=$false;UiCProductAccepted=$false}
$taskResult|ConvertTo-Json -Depth 45|Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if(!$taskPassed){throw "Pointer path check did not pass; preserve evidence: $taskOutput"}
$taskResult|Select-Object Passed,ArtifactsStable,NativePointer,NativeButtons
