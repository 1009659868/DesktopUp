#requires -Version 5.1
param([string]$CandidateDirectory,[string]$ReviewDirectory)
$ErrorActionPreference='Stop'
$nativeRepo=Split-Path -Parent $PSScriptRoot
if(!$CandidateDirectory){$CandidateDirectory=Join-Path $nativeRepo 'out/native-actions-revision/candidate04'}
if(!$ReviewDirectory){$ReviewDirectory=Join-Path $nativeRepo 'out/drop-d3/content-native-review04'}
$nativeReview=[IO.Path]::GetFullPath($ReviewDirectory).TrimEnd('\')
$nativeAllowed=[IO.Path]::GetFullPath((Join-Path $nativeRepo 'out/drop-d3')).TrimEnd('\')+'\'
if(!$nativeReview.StartsWith($nativeAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Review must stay inside repository/out/drop-d3.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
$nativeApp=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
$nativeFixture=Join-Path $CandidateDirectory 'fixture/DesktopUp.SessionFixture.exe'
if(!(Test-Path -LiteralPath $nativeApp) -or !(Test-Path -LiteralPath $nativeFixture)){throw 'Build the N4 candidate and fixture first; see CONTENT_NATIVE_ACTIONS.md.'}
$nativeWorker=Join-Path $CandidateDirectory 'app/content-worker'
foreach($nativeWorkerFile in @('DesktopUp.ContentWorker.exe','DesktopUp.ContentWorker.dll','DesktopUp.ContentWorker.runtimeconfig.json')){
    if(!(Test-Path -LiteralPath (Join-Path $nativeWorker $nativeWorkerFile))){throw "Incomplete bundled content-worker: $nativeWorkerFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$nativeTools=Enter-DesktopUpToolchain -Offline
try {
    $nativeProfile=Join-Path $nativeReview 'profile'
    $nativeMarker=Join-Path $nativeReview '.native-actions-review-ready'
    if(!(Test-Path -LiteralPath $nativeReview)){
        New-Item -ItemType Directory -Path $nativeReview | Out-Null
        [IO.File]::WriteAllText((Join-Path $nativeReview '.incoming-fixture'),'owned')
        & $nativeFixture $nativeProfile --content-layout empty | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Review fixture creation failed.'}
        $seed=Start-DesktopUpFixtureProcess -ExecutablePath $nativeApp -DataRoot $nativeProfile -Arguments '--incoming-probe' -WindowStyle Hidden
        if(!$seed.WaitForExit(45000)){throw "Fixture not finished; PID $($seed.Id) preserved."}
        $events=@(Get-Content -LiteralPath (Join-Path $nativeProfile 'logs/desktopup.jsonl') | ForEach-Object {$_|ConvertFrom-Json})
        if($seed.ExitCode -ne 0 -or !($events | Where-Object eventName -eq 'contract.incoming_passed')){throw 'Fixture preparation failed; choose a new review directory. Existing data preserved.'}
        foreach($folder in @('mapped-directory','incoming-files','outgoing-target')){New-Item -ItemType Directory -Path (Join-Path $nativeReview $folder) | Out-Null}
        foreach($relative in @('mapped-directory/keep.txt','mapped-directory/collision.txt','incoming-files/move.txt','incoming-files/copy.txt','incoming-files/collision.txt')){
            [IO.File]::WriteAllText((Join-Path $nativeReview $relative),'N4 isolated sample: '+$relative)
        }
        New-Item -ItemType Directory -Path (Join-Path $nativeReview 'incoming-files/folder') | Out-Null
        [IO.File]::WriteAllText((Join-Path $nativeReview 'incoming-files/folder/child.txt'),'N4 isolated child')
        [IO.File]::WriteAllText($nativeMarker,'owned')
    }
    if(!(Test-Path -LiteralPath $nativeMarker) -or [IO.File]::ReadAllText($nativeMarker) -ne 'owned'){throw 'Not a completed N4 fixture; no data overwritten.'}
    $app=Start-DesktopUpFixtureProcess -ExecutablePath $nativeApp -DataRoot $nativeProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{ProcessId=$app.Id;Profile=$nativeProfile;MappedDirectory=(Join-Path $nativeReview 'mapped-directory');IncomingFiles=(Join-Path $nativeReview 'incoming-files');OutgoingTarget=(Join-Path $nativeReview 'outgoing-target');Executable=$nativeApp}
} finally {Exit-DesktopUpToolchain $nativeTools}
