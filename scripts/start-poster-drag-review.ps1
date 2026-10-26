#requires -Version 5.1
param(
    [ValidateSet('Open','Prepare','Probe','Status')][string]$Action='Open',
    [ValidateRange(1,99)][int]$Run=1
)
$ErrorActionPreference='Stop'
$dragRepo=Split-Path -Parent $PSScriptRoot
$dragMode=if($Action -eq 'Probe'){'probe'}else{'review'}
$dragRoot=Join-Path $dragRepo ('out/drop-d3/drag-presentation-'+$dragMode+$Run.ToString('00'))
$dragProfile=Join-Path $dragRoot 'profile'
$dragCandidate=if($Action -eq 'Probe'){'probe'}else{'candidate'}
$dragApp=Join-Path $dragRepo ('out/poster-drag-presentation/'+$dragCandidate+'/app/DesktopUp.exe')
$dragFixture=Join-Path $dragRepo 'tests/DesktopUp.SessionFixture/bin/x64/Release/net10.0-windows10.0.22621.0/DesktopUp.SessionFixture.exe'
$dragMarker=Join-Path $dragRoot '.drag-presentation-fixture'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$dragApp;Profile=$dragProfile;Samples=$dragRoot;Prepared=(Test-Path -LiteralPath $dragMarker)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw '请先从托盘正常退出 DesktopUp；未结束任何进程。'}
foreach($dragFile in @($dragApp,$dragFixture)){
    if(!(Test-Path -LiteralPath $dragFile -PathType Leaf)){throw "缺少独立构建：$dragFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$dragTools=Enter-DesktopUpToolchain -Offline
try{
    if($Action -eq 'Probe' -and (Test-Path -LiteralPath $dragRoot)){throw '已有测量证据，请使用新的 -Run；未覆盖。'}
    if(!(Test-Path -LiteralPath $dragRoot)){
        New-Item -ItemType Directory -Path $dragRoot | Out-Null
        [IO.File]::WriteAllText((Join-Path $dragRoot '.cross-poster-fixture'),'owned')
        & $dragFixture $dragProfile --cross-poster | Out-Null
        if($LASTEXITCODE -ne 0){throw '隔离样例初始化失败，保留现场。'}
        [IO.File]::WriteAllText($dragMarker,'owned')
        if($Action -eq 'Probe'){[IO.File]::WriteAllText((Join-Path $dragRoot '.heat-runtime-fixture'),'owned')}
    }
    if(!(Test-Path -LiteralPath $dragMarker) -or [IO.File]::ReadAllText($dragMarker) -ne 'owned'){
        throw '不是已完成的本轮夹具；未覆盖。'
    }
    if($Action -eq 'Prepare'){[pscustomobject]@{Profile=$dragProfile;Samples=$dragRoot};return}
    $dragArguments=if($Action -eq 'Probe'){'--content-heat-runtime-probe --drag-presentation-probe'}else{'--show-control-center'}
    $dragProcess=Start-DesktopUpFixtureProcess -ExecutablePath $dragApp -DataRoot $dragProfile -Arguments $dragArguments -WindowStyle Hidden
    if($Action -ne 'Probe'){
        [pscustomobject]@{PID=$dragProcess.Id;Profile=$dragProfile;Samples=$dragRoot;InstallerExecuted=$false};return
    }
    if(!$dragProcess.WaitForExit(45000)){throw "测量超时，保留现场；PID $($dragProcess.Id)。"}
    $dragEvents=@(Get-Content -LiteralPath (Join-Path $dragProfile 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    $dragResult=@($dragEvents | Where-Object eventName -eq 'contract.drag_presentation_passed')
    $dragStopped=@($dragEvents | Where-Object eventName -eq 'application.stopped')
    if($dragProcess.ExitCode -ne 0 -or $dragResult.Count -ne 1 -or $dragStopped.Count -ne 1 -or
        $dragStopped[0].data.activeResources -ne 0 -or @($dragEvents | Where-Object level -in @('error','critical','fatal')).Count){
        throw "测量或安全检查失败；请查看 $dragProfile"
    }
    $dragResult[0].data | ConvertTo-Json -Depth 8
}finally{Exit-DesktopUpToolchain $dragTools}
