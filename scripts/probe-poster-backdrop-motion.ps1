#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$DatabaseCopy,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [switch]$SecondPoster
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp=(Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskDatabase=(Resolve-Path -LiteralPath $DatabaseCopy).Path
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach($taskProtected in @($taskApp,$taskNormal)) {
    if($taskOutput.Equals($taskProtected,[StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Use an isolated output directory.'}
}
if(Test-Path -LiteralPath $taskOutput){throw 'Preserve existing evidence; choose a new output directory.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process was stopped.'}
if(Test-Path -LiteralPath ($taskDatabase+'-wal')) {
    if((Get-Item -LiteralPath ($taskDatabase+'-wal')).Length -gt 0) {throw 'Supply a checkpointed database copy without a live WAL.'}
}
$taskAssembly=Join-Path $taskApp 'DesktopUp.dll'
if((Get-FileHash -LiteralPath $taskAssembly).Hash -ine $ExpectedAssemblySha256){throw 'Candidate hash differs.'}
function Get-BackdropMotionBinding {
    @(Get-ChildItem -LiteralPath $taskApp -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($taskApp.Length).TrimStart('\').Replace('\','/');
            Length=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$taskBinding=Get-BackdropMotionBinding
$taskScriptBinding=@($PSCommandPath,(Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')) | ForEach-Object {
    [pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_).Hash}
}
$taskSourceHash=(Get-FileHash -LiteralPath $taskDatabase).Hash
$taskData=Join-Path $taskOutput 'data'
New-Item -ItemType Directory -Path (Join-Path $taskData 'state') | Out-Null
Copy-Item -LiteralPath $taskDatabase -Destination (Join-Path $taskData 'state\desktopup.sqlite3')
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskScriptBinding | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $taskOutput 'scripts.json') -Encoding UTF8
$taskArguments='--backdrop-probe --backdrop-motion-capture --exit-after-ms=60000'
if($SecondPoster){$taskArguments+=' --backdrop-second-poster'}
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskApp 'DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskDeadline=[DateTime]::UtcNow.AddSeconds(65)
while(!$taskProcess.WaitForExit(500)) {
    if([DateTime]::UtcNow -gt $taskDeadline){throw 'Probe timed out; process and evidence preserved. No process was stopped.'}
}
$taskFailures=[Collections.Generic.List[string]]::new()
$taskComparisons=@(); $taskPhases=@(); $taskReport=$null; $taskEvents=@(); $taskStopped=@(); $taskErrors=@()
try {
    $taskReport=Get-Content -LiteralPath (Join-Path $taskData 'backdrop-observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') -Encoding UTF8 | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $taskProcess.Id)
    $taskErrors=@($taskEvents | Where-Object level -in @('error','critical','fatal'))
    $taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
    if(!$taskReport.passed){$taskFailures.Add('Native capture probe failed: '+$taskReport.error)}
    if(!$taskReport.phasePaced -or $taskReport.originalSpeedCertified -or $taskReport.animationFramesCertified){$taskFailures.Add('Capture scope metadata is invalid.')}
    if([bool]$taskReport.secondPoster -ne [bool]$SecondPoster){$taskFailures.Add('Selected poster mode differs from the requested mode.')}
    if(!$SecondPoster -and $taskReport.posterId -cne 'poster-main'){$taskFailures.Add('Primary poster selection differs.')}
    if($SecondPoster -and $taskReport.posterId -ceq 'poster-main'){$taskFailures.Add('Second poster selection differs.')}
    $taskPhases=@($taskReport.motionFrames)
    if($taskPhases.Count -ne 3){$taskFailures.Add('Expected exactly three intermediate phase captures.')}
    $taskReferenceRow=@($taskReport.rows | Where-Object name -eq 'erase-redraw')
    if($taskReferenceRow.Count -ne 1){throw 'Expected one explicitly erased final reference.'}
    $taskReferenceRow=$taskReferenceRow[0]
    $taskBackdrop=$taskReferenceRow.readback.BackdropColor
    if(!$taskReferenceRow.readback.BackdropValid -or $taskBackdrop -notmatch '^#[0-9A-Fa-f]{8}$'){throw 'Final reference has no valid native backdrop.'}
    Add-Type -AssemblyName System.Drawing
    $taskReference=[Drawing.Bitmap]::new((Join-Path $taskData 'backdrop-erase-redraw.png'))
    try {
        $taskPreviousHeight=0; $taskPreviousFrame=0; $taskRequestId=$null
        $taskStageIndex=0
        foreach($taskPhase in $taskPhases) {
            $taskFrame=$taskPhase.Frame
            $taskHeight=[int]$taskFrame.VisibleBounds.Height; $taskWidth=[int]$taskFrame.VisibleBounds.Width
            $taskInitialHeight=[int]$taskFrame.InitialBounds.Height; $taskFinalHeight=[int]$taskFrame.FinalBounds.Height
            $taskExpectedStage=@('early','middle','late')[$taskStageIndex]
            $taskMinimumProgress=@(.2,.5,.8)[$taskStageIndex]
            $taskMaximumProgress=@(.4,.7,1.0)[$taskStageIndex]
            if($taskPhase.Name -cne $taskExpectedStage){throw 'Phase order differs.'}
            $taskProgress=($taskHeight-$taskInitialHeight)/[double]($taskFinalHeight-$taskInitialHeight)
            if($taskHeight -le $taskPreviousHeight -or $taskHeight -lt 100 -or $taskHeight -le $taskInitialHeight -or $taskHeight -ge $taskFinalHeight -or
                $taskProgress -lt $taskMinimumProgress -or $taskProgress -ge $taskMaximumProgress) {throw 'Capture is not a distinct early/middle/late intermediate body height.'}
            if($taskFrame.Collapsed -or $taskFrame.PosterId -cne $taskReport.posterId -or $taskFrame.AcceptedFrame -le $taskPreviousFrame) {throw 'Phase is not an advancing expansion of the selected poster.'}
            if($null -ne $taskRequestId -and $taskRequestId -cne $taskFrame.RequestId){throw 'Phases came from different posture commands.'}
            $taskRequestId=$taskFrame.RequestId
            foreach($taskNativeRegion in @($taskPhase.NativeRegionBefore,$taskPhase.NativeRegionAfter)) {
                if($taskNativeRegion.X -ne 0 -or $taskNativeRegion.Y -ne 0 -or $taskNativeRegion.Width -ne $taskWidth -or $taskNativeRegion.Height -ne $taskHeight){throw 'Native HRGN did not hold the reported phase.'}
            }
            foreach($taskReadback in @($taskPhase.Before,$taskPhase.After)) {
                if(!$taskReadback.BackdropValid -or $taskReadback.BackdropColor -cne $taskBackdrop){throw 'Native backdrop color/alpha differs from final reference.'}
            }
            if($taskWidth -ne $taskReferenceRow.visibleBounds.Width -or $taskFinalHeight -ne $taskReferenceRow.visibleBounds.Height -or
                $taskReference.Width -lt $taskWidth -or $taskReference.Height -lt $taskFinalHeight) {throw 'Final reference does not cover the sampled geometry.'}
            $taskBitmap=[Drawing.Bitmap]::new((Join-Path $taskData ('backdrop-motion-'+$taskExpectedStage+'.png')))
            try {
                # WGC may keep the full prepared allocation. Only pixels within the held native
                # HRGN count. The side gutters avoid cards/text/beam and are newly exposed body.
                if($taskBitmap.Width -lt $taskWidth -or $taskBitmap.Height -lt $taskHeight){throw 'Capture does not cover its visible HRGN.'}
                $taskY=$taskHeight-20
                if($taskY -le $taskInitialHeight){throw 'Sample does not lie below the initial collapsed region.'}
                foreach($taskX in @(10,($taskWidth-10))) {
                    $taskActual=$taskBitmap.GetPixel($taskX,$taskY); $taskExpected=$taskReference.GetPixel($taskX,$taskY)
                    $taskDeltas=@([Math]::Abs([int]$taskActual.A-$taskExpected.A),[Math]::Abs([int]$taskActual.R-$taskExpected.R),
                        [Math]::Abs([int]$taskActual.G-$taskExpected.G),[Math]::Abs([int]$taskActual.B-$taskExpected.B))
                    $taskDelta=($taskDeltas | Measure-Object -Maximum).Maximum
                    $taskComparisons += [pscustomobject]@{Name=$taskExpectedStage;VisibleHeight=$taskHeight;X=$taskX;Y=$taskY;
                        Actual=$taskActual.ToArgb();Expected=$taskExpected.ToArgb();ChannelDeltas=$taskDeltas;Delta=$taskDelta;Passed=($taskDelta -le 2)}
                }
            } finally {$taskBitmap.Dispose()}
            $taskPreviousHeight=$taskHeight; $taskPreviousFrame=$taskFrame.AcceptedFrame; $taskStageIndex++
        }
    } finally {$taskReference.Dispose()}
} catch {$taskFailures.Add($_.Exception.ToString())}
$taskStable=($taskBinding | ConvertTo-Json -Compress) -ceq ((Get-BackdropMotionBinding) | ConvertTo-Json -Compress)
$taskScriptsStable=@($taskScriptBinding | Where-Object {(Get-FileHash -LiteralPath $_.Path).Hash -cne $_.SHA256}).Count -eq 0
$taskSourceStable=(Get-FileHash -LiteralPath $taskDatabase).Hash -eq $taskSourceHash
if($taskProcess.ExitCode -ne 0){$taskFailures.Add('Application exit code was nonzero.')}
if(!$taskStable -or !$taskScriptsStable -or !$taskSourceStable){$taskFailures.Add('Candidate, script, or source database changed during the probe.')}
if($taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0){$taskFailures.Add('Expected one clean application stop with zero active resources.')}
if($taskErrors.Count -ne 0){$taskFailures.Add('Application logged errors during the probe.')}
if($taskComparisons.Count -ne 6 -or @($taskComparisons | Where-Object {!$_.Passed}).Count -ne 0){$taskFailures.Add('Intermediate native pixels differ from the final erased reference, or samples are missing.')}
$taskPassed=$taskFailures.Count -eq 0
$taskResult=[pscustomobject]@{Passed=$taskPassed;ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;
    PosterId=$taskReport.posterId;SecondPoster=[bool]$SecondPoster;ExpectedAssemblySha256=$ExpectedAssemblySha256;
    ArtifactsStable=$taskStable;ScriptsStable=$taskScriptsStable;SourceDatabaseUnchanged=$taskSourceStable;
    SourceDatabaseSha256=$taskSourceHash;BackdropColor=$taskBackdrop;PhaseHeights=@($taskPhases | ForEach-Object {$_.Frame.VisibleBounds.Height});
    PixelComparisons=$taskComparisons;Errors=$taskErrors;Failures=@($taskFailures);Cleanup=@($taskStopped.data);
    Scope='Own HWND native composition; three phase-paced expansion checkpoints';PhasePaced=$true;
    NativeMouse=$false;OriginalSpeedCertified=$false;AnimationFramesCertified=$false;MilestoneAccepted=$false}
$taskResult | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if(!$taskPassed){throw 'Phase-paced native backdrop regression failed; evidence preserved.'}
$taskResult | Select-Object Passed,PosterId,BackdropColor,PhaseHeights,ArtifactsStable,SourceDatabaseUnchanged,Scope
