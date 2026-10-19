#requires -Version 5.1
# Shared R3/R4 launcher: existing fixtures/probes with isolated builds and profiles.
param(
    [ValidateSet('Open','Prepare','Status','Probe')][string]$Action='Open',
    [ValidateSet('Presentation','Source','Offline')][string]$Probe='Presentation',
    [ValidateRange(1,99)][int]$Run=1,
    [ValidateSet('R3','R4')][string]$Batch='R3'
)
$ErrorActionPreference='Stop'
$reviewRepo=Split-Path -Parent $PSScriptRoot
$reviewBatchName=if($Batch -eq 'R4'){'architecture-r4-composition'}else{'architecture-r3-presentation'}
$reviewCandidate=Join-Path $reviewRepo ("out/$reviewBatchName/candidate01")
$reviewProduct=Join-Path $reviewCandidate 'app'
$reviewReview=Join-Path $reviewRepo ("out/drop-d3/$reviewBatchName-review01")
$reviewFixture=Join-Path $reviewCandidate 'fixture/DesktopUp.SessionFixture.exe'
if($Action -eq 'Probe'){
    $reviewProduct=Join-Path $reviewRepo ("out/$reviewBatchName/probes/app")
    $reviewReview=Join-Path $reviewRepo (("out/drop-d3/$reviewBatchName-probe-")+$Probe.ToLowerInvariant()+$Run.ToString('00'))
}
$reviewApp=Join-Path $reviewProduct 'DesktopUp.exe'
$reviewProfile=Join-Path $reviewReview 'profile'
$reviewMarker=Join-Path $reviewReview '.cross-poster-fixture'
$reviewReady=Join-Path $reviewReview '.presentation-review-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$reviewApp;Profile=$reviewProfile;Samples=$reviewReview;Prepared=(Test-Path -LiteralPath $reviewReady)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
foreach($reviewPath in @($reviewApp,$reviewFixture)){
    if(!(Test-Path -LiteralPath $reviewPath -PathType Leaf)){throw "Missing build: $reviewPath"}
}
if($Action -ne 'Probe'){
    $reviewHashes=@{
        'DesktopUp.dll'='3BEB8E8AA1994FEA97113705A6C897118D3444FEFCC3BDF71B069C1614F172F1'
        'DesktopUp.Application.dll'='D8BDF0ABF990011D42998C5F5041849C230CA1A2516C718B248E3BF9A7EEFB46'
        'DesktopUp.Domain.dll'='661882EFFCA129A1C8ECAA160291FE301B8795BD2CC59B4ED8A8D6F1B50FDB25'
    }
    if($Batch -eq 'R4'){
        $reviewHashes=Get-Content -LiteralPath (Join-Path $reviewCandidate 'candidate-hashes.json') -Raw | ConvertFrom-Json
        $reviewMap=@{}; $reviewHashes.PSObject.Properties | ForEach-Object {$reviewMap[$_.Name]=$_.Value}; $reviewHashes=$reviewMap
    }
    foreach($reviewName in $reviewHashes.Keys){
        if((Get-FileHash -LiteralPath (Join-Path $reviewProduct $reviewName)).Hash -cne $reviewHashes[$reviewName]){
            throw "Candidate changed: $reviewName. Nothing overwritten."
        }
    }
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$reviewTools=Enter-DesktopUpToolchain -Offline
try {
    if($Action -eq 'Probe'){
        if(Test-Path -LiteralPath $reviewReview){throw 'Probe evidence already exists; nothing overwritten.'}
        New-Item -ItemType Directory -Path $reviewReview | Out-Null
        if($Probe -eq 'Presentation'){
            [IO.File]::WriteAllText($reviewMarker,'owned')
            [IO.File]::WriteAllText((Join-Path $reviewReview '.heat-runtime-fixture'),'owned')
            & $reviewFixture $reviewProfile --cross-poster | Out-Null
            $reviewArguments='--content-heat-runtime-probe --r3-presentation-probe'
            $reviewEvent='contract.posture_rasters_passed'
        }elseif($Probe -eq 'Offline'){
            New-Item -ItemType Directory -Path $reviewProfile | Out-Null
            $reviewArguments='--recover-state --recovery-smoke'
            $reviewEvent='contract.recovery_smoke'
            $LASTEXITCODE=0
        }else{
            & $reviewFixture $reviewProfile --content-layout resource-one | Out-Null
            $reviewArguments='--content-source-probe'
            $reviewEvent='content.source_observed'
        }
        if($LASTEXITCODE-ne 0){throw 'Probe fixture failed; evidence retained.'}
        $reviewHash=(Get-FileHash -LiteralPath (Join-Path $reviewProduct 'DesktopUp.dll')).Hash
        $reviewProcess=Start-DesktopUpFixtureProcess -ExecutablePath $reviewApp -DataRoot $reviewProfile -Arguments $reviewArguments -WindowStyle Hidden
        Write-Host "Probe $Probe started: PID $($reviewProcess.Id)"
        $reviewDeadline=[DateTime]::UtcNow.AddSeconds(90)
        while(!$reviewProcess.WaitForExit(500)){
            if([DateTime]::UtcNow -gt $reviewDeadline){throw "Probe timeout; PID $($reviewProcess.Id) and evidence retained."}
        }
        $reviewEvents=@(Get-Content -LiteralPath (Join-Path $reviewProfile 'logs/desktopup.jsonl') | ForEach-Object {$_|ConvertFrom-Json})
        $reviewDone=@($reviewEvents|Where-Object eventName -eq $reviewEvent)
        $reviewStopped=@($reviewEvents|Where-Object eventName -eq 'application.stopped')
        $reviewErrors=@($reviewEvents|Where-Object level -in @('error','critical','fatal'))
        if($reviewProcess.ExitCode-ne 0 -or $reviewDone.Count-ne 1 -or $reviewStopped.Count-ne 1 -or
            $reviewStopped[0].data.activeResources-ne 0 -or $reviewErrors.Count){throw "Probe failed; inspect $reviewProfile"}
        if($Probe -eq 'Offline'){
            if(!$reviewDone[0].data.passed -or (Test-Path -LiteralPath (Join-Path $reviewProfile 'state/desktopup.sqlite3')) -or
                @($reviewEvents|Where-Object eventName -eq 'application.started').Count){throw 'Offline mode opened live services.'}
        }
        if($Probe -eq 'Presentation'){
            foreach($reviewExpected in @('contract.r3_frame_publication_passed','contract.incremental_render_passed','contract.incoming_hover_passed')){
                if(@($reviewEvents|Where-Object eventName -eq $reviewExpected).Count-ne 1){throw "Missing probe result: $reviewExpected"}
            }
        }
        if((Get-FileHash -LiteralPath (Join-Path $reviewProduct 'DesktopUp.dll')).Hash -cne $reviewHash){throw 'Probe build changed.'}
        $reviewResult=[pscustomobject]@{Result='PASS';Probe=$Probe;PID=$reviewProcess.Id;ResourcesAfterExit=0;NativeMouse=$false;AppHash=$reviewHash}
        $reviewResult|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reviewReview 'results.json') -Encoding UTF8
        $reviewResult
        return
    }
    if(!(Test-Path -LiteralPath $reviewReview)){
        New-Item -ItemType Directory -Path $reviewReview | Out-Null
        [IO.File]::WriteAllText($reviewMarker,'owned')
        & $reviewFixture $reviewProfile --cross-poster | Out-Null
        if($LASTEXITCODE-ne 0){throw 'Fixture creation failed; partial evidence retained.'}
        Get-ChildItem -LiteralPath $reviewReview -File -Recurse | Where-Object {$_.FullName -notlike ($reviewProfile+'*')} |
            ForEach-Object {Get-FileHash -LiteralPath $_.FullName} |
            ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $reviewReview 'sample-manifest.json') -Encoding UTF8
        [IO.File]::WriteAllText($reviewReady,'owned')
    }
    if(!(Test-Path -LiteralPath $reviewReady) -or [IO.File]::ReadAllText($reviewMarker)-ne 'owned'){
        throw 'Not a completed owned review. Nothing overwritten.'
    }
    if($Action -eq 'Prepare'){[pscustomobject]@{Profile=$reviewProfile;Samples=$reviewReview};return}
    $reviewProcess=Start-DesktopUpFixtureProcess -ExecutablePath $reviewApp -DataRoot $reviewProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$reviewProcess.Id;Profile=$reviewProfile;Samples=$reviewReview;InstallerExecuted=$false}
}finally{Exit-DesktopUpToolchain $reviewTools}
