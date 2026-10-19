#requires -Version 7
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$DatabaseCopy,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256,
    [switch]$ScreenCrop,
    [switch]$ScreenOnly
)
$ErrorActionPreference='Stop'
if($ScreenCrop -and $ScreenOnly){throw 'Choose paired WGC/screen or screen-only capture, not both.'}
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp=(Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskDatabase=(Resolve-Path -LiteralPath $DatabaseCopy).Path
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskOutBase=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../out')).TrimEnd('\')+'\'
if(!$taskOutput.StartsWith($taskOutBase,[StringComparison]::OrdinalIgnoreCase)) {throw 'Evidence must use a new project out directory.'}
if($taskOutput.Equals($taskApp,[StringComparison]::OrdinalIgnoreCase) -or
    $taskOutput.StartsWith($taskApp+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Do not place evidence inside the candidate.'}
if(Test-Path -LiteralPath $taskOutput){throw 'Preserve existing evidence; choose a new directory.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process was stopped.'}
if((Test-Path -LiteralPath ($taskDatabase+'-wal')) -and (Get-Item -LiteralPath ($taskDatabase+'-wal')).Length -gt 0) {throw 'Use a checkpointed copy, not a live database.'}
if((Get-FileHash -LiteralPath (Join-Path $taskApp 'DesktopUp.dll')).Hash -ine $ExpectedAssemblySha256){throw 'Candidate differs.'}
function Get-LiveBinding {
    @(Get-ChildItem -LiteralPath $taskApp -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($taskApp.Length);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$taskBinding=Get-LiveBinding
$taskScriptBinding=@($PSCommandPath,(Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')) | ForEach-Object {
    [pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_).Hash}
}
$taskSourceHash=(Get-FileHash -LiteralPath $taskDatabase).Hash
$taskData=Join-Path $taskOutput 'data'
New-Item -ItemType Directory -Path (Join-Path $taskData 'state') | Out-Null
Copy-Item -LiteralPath $taskDatabase -Destination (Join-Path $taskData 'state/desktopup.sqlite3')
$taskBinding | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskScriptBinding | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput 'scripts.json') -Encoding UTF8
$taskArguments='--backdrop-probe --backdrop-live-capture --exit-after-ms=60000'
if($ScreenCrop){$taskArguments+=' --backdrop-screen-crop'}
if($ScreenOnly){$taskArguments+=' --backdrop-screen-only'}
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskApp 'DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
$taskDeadline=[DateTime]::UtcNow.AddSeconds(65)
while(!$taskProcess.WaitForExit(500)) {if([DateTime]::UtcNow -gt $taskDeadline){throw 'Timeout; process and evidence preserved, no force termination.'}}
$taskReport=Get-Content -LiteralPath (Join-Path $taskData 'backdrop-observation.json') -Raw | ConvertFrom-Json
$taskFrames=Get-Content -LiteralPath (Join-Path $taskData 'live-expansion/frames.json') -Raw | ConvertFrom-Json
$taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $taskProcess.Id)
$taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
$taskErrors=@($taskEvents | Where-Object level -in @('error','critical','fatal'))
$taskStable=($taskBinding | ConvertTo-Json -Compress) -ceq ((Get-LiveBinding) | ConvertTo-Json -Compress)
$taskSourceStable=(Get-FileHash -LiteralPath $taskDatabase).Hash -ceq $taskSourceHash
$taskModeMatches=if($ScreenOnly){$taskReport.screenOnly -and $taskFrames.screenOnly -and !$taskReport.ownWindowCapture}else{
    !$taskReport.screenOnly -and !$taskFrames.screenOnly -and $taskReport.ownWindowCapture -and ([bool]$taskFrames.screenCropOwnBounds -eq [bool]$ScreenCrop)
}
$taskPassed=$taskReport.passed -and $taskFrames.frames.Count -ge 2 -and !$taskFrames.error -and !$taskFrames.phasePaced -and
    $taskProcess.ExitCode -eq 0 -and $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and $taskErrors.Count -eq 0 -and $taskStable -and $taskSourceStable -and $taskModeMatches
$taskResult=[pscustomobject]@{CaptureCompleted=$taskPassed;VisualAccepted=$false;NativeMouse=$false;ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;
    ExpectedAssemblySha256=$ExpectedAssemblySha256;SourceSHA256=$taskSourceHash;SourceUnchanged=$taskSourceStable;ArtifactsStable=$taskStable;
    Frames=$taskFrames.frames.Count;Skipped=$taskFrames.skipped;OverBudget=$taskFrames.overBudget;Bytes=$taskFrames.bytes;
    ScreenOccluded=$taskFrames.screenOccluded;ModeMatches=[bool]$taskModeMatches;
    Error=$taskReport.error;Errors=$taskErrors;Cleanup=$taskStopped.data;ScreenCropOwnBounds=[bool]($ScreenCrop -or $ScreenOnly);ScreenOnly=[bool]$ScreenOnly;
    Scope='Unpaced bounded capture; screen may include occluders; screen-only avoids WGC; no mode certifies every presented frame'}
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'observation.json') -Encoding UTF8
$taskResult
if(!$taskPassed){throw 'Live capture failed; evidence preserved.'}
