#requires -Version 5.1
param(
    [ValidateSet('Open','Prepare','Status','Probe')][string]$Action='Open',
    [ValidateRange(1,99)][int]$Run=2,
    [ValidateRange(1,2)][int]$Candidate=2,
    [ValidateSet('Presentation','Scale')][string]$Probe='Scale'
)
$ErrorActionPreference='Stop'
$reviewRepo=Split-Path -Parent $PSScriptRoot
$reviewCandidate=Join-Path $reviewRepo ('out/poster-scale-and-hover/candidate'+$Candidate.ToString('00'))
$reviewApp=Join-Path $reviewCandidate 'app/DesktopUp.exe'
$reviewFixture=Join-Path $reviewRepo 'out/poster-scale-and-hover/candidate01/fixture/DesktopUp.SessionFixture.exe'
$reviewName=if($Action -eq 'Probe' -and $Probe -eq 'Scale'){'poster-scale-expansion-probe'}elseif($Action -eq 'Probe'){'poster-scale-probe'}else{'poster-scale-review'}
$reviewRoot=Join-Path $reviewRepo ('out/drop-d3/'+$reviewName+$Run.ToString('00'))
$reviewProfile=Join-Path $reviewRoot 'profile'
$reviewMarker=Join-Path $reviewRoot '.cross-poster-fixture'
$reviewReady=Join-Path $reviewRoot '.scale-review-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$reviewApp;Profile=$reviewProfile;Samples=$reviewRoot;Prepared=(Test-Path -LiteralPath $reviewReady)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw '请先从托盘正常退出 DesktopUp；未结束任何进程。'}
foreach($reviewFile in @($reviewApp,$reviewFixture)){
    if(!(Test-Path -LiteralPath $reviewFile -PathType Leaf)){throw "缺少独立构建：$reviewFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$reviewTools=Enter-DesktopUpToolchain -Offline
try{
    if($Action -eq 'Probe' -and (Test-Path -LiteralPath $reviewRoot)){
        throw '已有检查证据；未覆盖。请指定新的 -Run。'
    }
    if(!(Test-Path -LiteralPath $reviewRoot)){
        New-Item -ItemType Directory -Path $reviewRoot | Out-Null
        [IO.File]::WriteAllText($reviewMarker,'owned')
        if($Action -eq 'Probe'){[IO.File]::WriteAllText((Join-Path $reviewRoot '.heat-runtime-fixture'),'owned')}
        & $reviewFixture $reviewProfile --cross-poster | Out-Null
        if($LASTEXITCODE -ne 0){throw '隔离样例初始化失败，保留现场；未启动程序。'}
        [IO.File]::WriteAllText($reviewReady,'owned')
    }
    if(!(Test-Path -LiteralPath $reviewReady) -or [IO.File]::ReadAllText($reviewMarker) -ne 'owned'){
        throw '不是已完成的隔离样例；未覆盖。'
    }
    if($Action -eq 'Prepare'){[pscustomobject]@{Profile=$reviewProfile;Samples=$reviewRoot};return}
    $reviewArguments=if($Action -ne 'Probe'){'--show-control-center'}elseif($Probe -eq 'Scale'){
        '--content-heat-runtime-probe --scale-expansion-probe'
    }else{'--content-heat-runtime-probe --r3-presentation-probe'}
    $reviewProcess=Start-DesktopUpFixtureProcess -ExecutablePath $reviewApp -DataRoot $reviewProfile -Arguments $reviewArguments -WindowStyle Hidden
    if($Action -ne 'Probe'){
        [pscustomobject]@{PID=$reviewProcess.Id;Profile=$reviewProfile;Samples=$reviewRoot;InstallerExecuted=$false}
        return
    }
    if(!$reviewProcess.WaitForExit(45000)){throw "检查超时，保留现场；PID $($reviewProcess.Id)。"}
    $reviewEvents=@(Get-Content -LiteralPath (Join-Path $reviewProfile 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    $reviewExpected=if($Probe -eq 'Scale'){@('contract.scale_expansion_passed')}else{
        @('contract.content_hover_passed','contract.r3_frame_publication_passed','contract.incremental_render_passed','contract.incoming_hover_passed','contract.posture_rasters_passed')
    }
    foreach($reviewEvent in $reviewExpected){
        if(@($reviewEvents | Where-Object eventName -eq $reviewEvent).Count -ne 1){throw "缺少检查结果：$reviewEvent；请查看 $reviewProfile"}
    }
    $reviewStopped=@($reviewEvents | Where-Object eventName -eq 'application.stopped')
    if($reviewProcess.ExitCode -ne 0 -or $reviewStopped.Count -ne 1 -or $reviewStopped[0].data.activeResources -ne 0 -or
        @($reviewEvents | Where-Object level -in @('error','critical','fatal')).Count){throw "检查失败；请查看 $reviewProfile"}
    [pscustomobject]@{Result='PASS';NativeMouse=$false;ResourcesAfterExit=0;Profile=$reviewProfile}
}finally{Exit-DesktopUpToolchain $reviewTools}
